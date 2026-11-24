using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IJuegoTesoroRepositorio
{
    Task<RetoTesoro?> ObtenerSemana(DateOnly semana, CancellationToken ct = default);
    Task<IReadOnlyList<UbicacionTesoro>> ListarUbicaciones(CancellationToken ct = default);
    Task<UbicacionTesoro> GuardarUbicacion(UbicacionTesoro ubicacion, CancellationToken ct = default);
    Task<IReadOnlyList<ProgramacionTesoro>> ListarProgramaciones(DateOnly desde, DateOnly hasta, CancellationToken ct = default);
    Task GuardarProgramacion(ProgramacionTesoro programacion, CancellationToken ct = default);
    Task EliminarProgramacion(DateOnly semana, CancellationToken ct = default);
    Task ActualizarReto(RetoTesoro reto, CancellationToken ct = default);
    Task<RetoTesoro> ObtenerOCrearSemana(RetoTesoro candidato, CancellationToken ct = default);
    Task<HallazgoTesoro?> ObtenerHallazgo(int retoId, int usuarioId, CancellationToken ct = default);
    Task<HallazgoTesoro> GuardarHallazgo(HallazgoTesoro hallazgo, CancellationToken ct = default);
}
