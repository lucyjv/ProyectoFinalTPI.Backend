using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    IHttpClientFactory httpClientFactory,
    IOptions<MercadoPagoOptions> opcionesMercadoPago,
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

        var mpConfig = opcionesMercadoPago.Value;
        if (string.IsNullOrWhiteSpace(mpConfig.AccessToken) || mpConfig.AccessToken.StartsWith("REEMPLAZAR", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogError("No se puede iniciar el pago: MercadoPago:AccessToken no está configurado.");
            throw new InvalidOperationException("El servicio de cobro de Mercado Pago no está configurado en el servidor.");
        }

        var cliente = httpClientFactory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mpConfig.AccessToken);

        var esHttps = mpConfig.BaseUrlRetorno.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        var payload = new Dictionary<string, object?>
        {
            ["items"] = new[]
            {
                new
                {
                    id = pack.Id,
                    title = $"Nostalgiar - {pack.Nombre} ({pack.Puntos} Puntos)",
                    description = $"Recarga de {pack.Puntos} puntos de nostalgia para tu cuenta en Nostalgiar.",
                    quantity = 1,
                    currency_id = "ARS",
                    unit_price = precioArs
                }
            },
            ["back_urls"] = new
            {
                success = $"{mpConfig.BaseUrlRetorno.TrimEnd('/')}/perfil?puntos=exito&compraId={compra.Id}",
                failure = $"{mpConfig.BaseUrlRetorno.TrimEnd('/')}/perfil?puntos=fallo&compraId={compra.Id}",
                pending = $"{mpConfig.BaseUrlRetorno.TrimEnd('/')}/perfil?puntos=pendiente&compraId={compra.Id}"
            },
            ["external_reference"] = compra.Id.ToString()
        };

        if (esHttps)
        {
            payload["auto_return"] = "approved";
        }

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var respuesta = await cliente.PostAsync("https://api.mercadopago.com/checkout/preferences", jsonContent, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var err = await respuesta.Content.ReadAsStringAsync(ct);
            logger?.LogError("Mercado Pago respondió con error al crear preferencia ({StatusCode}): {Body}", (int)respuesta.StatusCode, err);
            throw new InvalidOperationException("Mercado Pago no pudo generar la preferencia de pago. Verifique las credenciales y la conexión.");
        }

        await using var stream = await respuesta.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        string preferenceId = string.Empty;
        if (doc.RootElement.TryGetProperty("id", out var idProp))
        {
            preferenceId = idProp.GetString() ?? string.Empty;
        }

        string initPoint = string.Empty;
        var esLocalhost = mpConfig.BaseUrlRetorno.Contains("localhost", StringComparison.OrdinalIgnoreCase);

        // Si estamos en entorno local de pruebas y Mercado Pago devolvió sandbox_init_point, lo priorizamos
        if (esLocalhost && doc.RootElement.TryGetProperty("sandbox_init_point", out var sandProp) && !string.IsNullOrWhiteSpace(sandProp.GetString()))
        {
            initPoint = sandProp.GetString()!;
        }
        else if (doc.RootElement.TryGetProperty("init_point", out var initProp) && !string.IsNullOrWhiteSpace(initProp.GetString()))
        {
            initPoint = initProp.GetString()!;
        }
        else if (doc.RootElement.TryGetProperty("sandbox_init_point", out var fallbackSandProp) && !string.IsNullOrWhiteSpace(fallbackSandProp.GetString()))
        {
            initPoint = fallbackSandProp.GetString()!;
        }

        if (string.IsNullOrWhiteSpace(initPoint))
        {
            logger?.LogError("Mercado Pago no devolvió una URL init_point válida.");
            throw new InvalidOperationException("Mercado Pago no devolvió un enlace de pago válido.");
        }

        compra.PreferenceId = preferenceId;
        await contexto.SaveChangesAsync(ct);

        return new PreferenciaCompraDto(
            compra.Id,
            preferenceId,
            initPoint,
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

        var mpConfig = opcionesMercadoPago.Value;
        if (string.IsNullOrWhiteSpace(mpConfig.AccessToken) || mpConfig.AccessToken.StartsWith("REEMPLAZAR", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning("No se puede consultar pago {PaymentId} a Mercado Pago porque AccessToken no está configurado.", paymentId);
            return null;
        }

        try
        {
            var cliente = httpClientFactory.CreateClient();
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mpConfig.AccessToken);

            using var respuesta = await cliente.GetAsync($"https://api.mercadopago.com/v1/payments/{paymentId}", ct);
            if (!respuesta.IsSuccessStatusCode)
            {
                logger?.LogWarning("Mercado Pago respondió {StatusCode} al consultar pago {PaymentId}", (int)respuesta.StatusCode, paymentId);
                return null;
            }

            await using var stream = await respuesta.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
            var extRef = root.TryGetProperty("external_reference", out var extProp) ? extProp.GetString() : null;

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
