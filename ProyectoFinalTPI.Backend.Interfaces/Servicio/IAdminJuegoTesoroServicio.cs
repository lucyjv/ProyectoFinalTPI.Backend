using ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IAdminJuegoTesoroServicio
{
    Task<PanelTesoroAdminDto> ObtenerPanel(int adminId, CancellationToken ct = default);
    Task<UbicacionTesoroDto> GuardarUbicacion(int adminId, int? id, GuardarUbicacionTesoroDto dto, CancellationToken ct = default);
    Task Programar(DateOnly semana, int? ubicacionId, CancellationToken ct = default);
    Task UsarRotacion(DateOnly semana, CancellationToken ct = default);
    Task EditarActual(int adminId, int retoId, EditarRetoTesoroDto dto, CancellationToken ct = default);
}
