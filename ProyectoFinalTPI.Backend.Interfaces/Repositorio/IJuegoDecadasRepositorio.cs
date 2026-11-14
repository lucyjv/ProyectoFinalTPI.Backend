using ProyectoFinalTPI.Backend.Dtos.JuegoDecadas;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IJuegoDecadasRepositorio
{
    Task<List<FotoCandidataJuegoDecadasDto>> ObtenerFotos(
        string usuario, string tituloExcluido, DateTime desde, DateTime hasta,
        CancellationToken cancellationToken = default);
}
