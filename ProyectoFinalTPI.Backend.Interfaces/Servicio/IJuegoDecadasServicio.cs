using ProyectoFinalTPI.Backend.Dtos.JuegoDecadas;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IJuegoDecadasServicio
{
    Task<IReadOnlyList<FotoJuegoDecadasDto>> ObtenerFotos(CancellationToken cancellationToken = default);
}
