using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IJuegoTesoroRepositorio
{
    Task<RetoTesoro> ObtenerOCrearSemana(RetoTesoro candidato, CancellationToken ct = default);
    Task<HallazgoTesoro?> ObtenerHallazgo(int retoId, int usuarioId, CancellationToken ct = default);
    Task<HallazgoTesoro> GuardarHallazgo(HallazgoTesoro hallazgo, CancellationToken ct = default);
}
