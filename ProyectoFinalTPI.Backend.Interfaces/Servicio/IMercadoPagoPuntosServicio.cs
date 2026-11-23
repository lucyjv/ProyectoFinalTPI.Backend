using ProyectoFinalTPI.Backend.Dtos.PuntosNostalgia;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IMercadoPagoPuntosServicio
{
    Task<IReadOnlyList<PackPuntosDto>> ObtenerPacksDisponiblesAsync(CancellationToken ct = default);
    Task<PreferenciaCompraDto> CrearPreferenciaCompraAsync(int usuarioId, string packId, CancellationToken ct = default);
    Task<ResultadoCompraDto> AcreditarCompraAsync(int compraId, string? paymentId = null, CancellationToken ct = default);
    Task<ResultadoCompraDto?> ProcesarNotificacionPagoAsync(string paymentId, CancellationToken ct = default);
}
