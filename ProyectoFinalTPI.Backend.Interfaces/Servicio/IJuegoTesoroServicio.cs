using ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IJuegoTesoroServicio
{
    Task<RetoTesoroDto?> ObtenerActual(int usuarioId, CancellationToken ct = default);
    Task<TesoroVisibleDto?> ObtenerVisible(int usuarioId, VistaMapaTesoroDto vista, CancellationToken ct = default);
    Task<HallazgoTesoroDto> Recoger(int usuarioId, int retoId, VistaMapaTesoroDto vista, CancellationToken ct = default);
}
