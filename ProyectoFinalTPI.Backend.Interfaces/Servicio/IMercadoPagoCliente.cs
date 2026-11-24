namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IMercadoPagoCliente
{
    Task<PreferenciaMercadoPago> CrearPreferenciaAsync(SolicitudPreferenciaMercadoPago solicitud, CancellationToken ct = default);
    Task<PagoMercadoPago?> ConsultarPagoAsync(string paymentId, CancellationToken ct = default);
}

public sealed record SolicitudPreferenciaMercadoPago(
    int CompraId, string PackId, string NombrePack, int Puntos, decimal PrecioArs);

public sealed record PreferenciaMercadoPago(string Id, string UrlPago);

public sealed record PagoMercadoPago(string? Estado, string? ReferenciaExterna);
