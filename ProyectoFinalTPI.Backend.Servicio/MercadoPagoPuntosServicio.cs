using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProyectoFinalTPI.Backend.Dtos.PuntosNostalgia;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class MercadoPagoPuntosServicio(
    ApplicationDbContext contexto,
    ICotizacionDolarServicio cotizacionServicio,
    IConsumoDiarioPuntosNostalgiaRepositorio consumos,
    ICupoPuntosNostalgiaServicio cupos,
    IMercadoPagoCliente mercadoPagoCliente,
    ILogger<MercadoPagoPuntosServicio>? logger = null) : IMercadoPagoPuntosServicio
{
    private static readonly IReadOnlyList<PackDefinicion> PacksCatalogo =
    [
        new("ficha-retro", "Ficha Retro", 15, 3.00m, "rail", "Ideal para seguir jugando hoy", false),
        new("cassette-nostalgico", "Cassette Nostálgico", 45, 7.00m, "cassette", "¡El más elegido! (+10 pts de regalo)", true),
        new("vinilo-coleccion", "Vinilo de Colección", 100, 12.00m, "vinyl", "Mejor valor (+25 pts de regalo)", false)
    ];

    public async Task<IReadOnlyList<PackPuntosDto>> ObtenerPacksDisponiblesAsync(CancellationToken ct = default)
    {
        var cotizacion = await cotizacionServicio.ObtenerCotizacionAsync(ct);
        return PacksCatalogo.Select(p => new PackPuntosDto(
            p.Id,
            p.Nombre,
            p.Puntos,
            p.PrecioUsd,
            Math.Round(p.PrecioUsd * cotizacion, 0),
            p.Icono,
            p.Etiqueta,
            p.Destacado
        )).ToList();
    }

    public async Task<PreferenciaCompraDto> CrearPreferenciaCompraAsync(int usuarioId, string packId, CancellationToken ct = default)
    {
        var pack = PacksCatalogo.FirstOrDefault(p => string.Equals(p.Id, packId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"El pack '{packId}' no existe en el catálogo.", nameof(packId));

        var cotizacion = await cotizacionServicio.ObtenerCotizacionAsync(ct);
        var precioArs = Math.Round(pack.PrecioUsd * cotizacion, 0);

        var compra = new CompraPuntosNostalgia
        {
            UsuarioId = usuarioId,
            PackId = pack.Id,
            Puntos = pack.Puntos,
            PrecioUsd = pack.PrecioUsd,
            CotizacionDolar = cotizacion,
            PrecioArs = precioArs,
            Estado = "Pendiente",
            FechaCreacion = DateTimeOffset.UtcNow
        };

        contexto.ComprasPuntosNostalgia.Add(compra);
        await contexto.SaveChangesAsync(ct);

        var preferencia = await mercadoPagoCliente.CrearPreferenciaAsync(
            new SolicitudPreferenciaMercadoPago(compra.Id, pack.Id, pack.Nombre, pack.Puntos, precioArs), ct);

        compra.PreferenceId = preferencia.Id;
        await contexto.SaveChangesAsync(ct);

        return new PreferenciaCompraDto(
            compra.Id,
            preferencia.Id,
            preferencia.UrlPago,
            pack.Id,
            pack.Nombre,
            pack.Puntos,
            precioArs,
            pack.PrecioUsd,
            cotizacion
        );
    }

    public async Task<ResultadoCompraDto> AcreditarCompraAsync(int compraId, string? paymentId = null, CancellationToken ct = default)
    {
        var compra = await contexto.ComprasPuntosNostalgia.FirstOrDefaultAsync(c => c.Id == compraId, ct)
            ?? throw new KeyNotFoundException($"No se encontró la compra con ID {compraId}.");

        if (compra.Estado == "Aprobado")
        {
            var saldoExistente = await cupos.ObtenerSaldo(compra.UsuarioId, ct);
            return new ResultadoCompraDto(compra.Id, compra.Estado, compra.Puntos, saldoExistente);
        }

        compra.Estado = "Aprobado";
        compra.PaymentId = paymentId ?? compra.PaymentId ?? $"PAY-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        compra.FechaAcreditacion = DateTimeOffset.UtcNow;

        await consumos.AcreditarPuntosExtra(compra.UsuarioId, compra.Puntos, ct);
        await contexto.SaveChangesAsync(ct);

        logger?.LogInformation("Compra {CompraId} acreditada exitosamente: +{Puntos} puntos para el usuario {UsuarioId}",
            compra.Id, compra.Puntos, compra.UsuarioId);

        var saldoActualizado = await cupos.ObtenerSaldo(compra.UsuarioId, ct);
        return new ResultadoCompraDto(compra.Id, compra.Estado, compra.Puntos, saldoActualizado);
    }

    public async Task<ResultadoCompraDto?> ProcesarNotificacionPagoAsync(string paymentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paymentId)) return null;

        try
        {
            var pago = await mercadoPagoCliente.ConsultarPagoAsync(paymentId, ct);
            if (pago is null) return null;

            var status = pago.Estado;
            var extRef = pago.ReferenciaExterna;

            if (string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase) && int.TryParse(extRef, out var compraId))
            {
                return await AcreditarCompraAsync(compraId, paymentId, ct);
            }

            logger?.LogInformation("Pago {PaymentId} no está aprobado o no contiene referencia válida (status: {Status}, extRef: {ExtRef})",
                paymentId, status, extRef);
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error al procesar notificación de pago {PaymentId} de Mercado Pago", paymentId);
            return null;
        }
    }

    private sealed record PackDefinicion(
        string Id, string Nombre, int Puntos, decimal PrecioUsd, string Icono, string Etiqueta, bool Destacado);
}
