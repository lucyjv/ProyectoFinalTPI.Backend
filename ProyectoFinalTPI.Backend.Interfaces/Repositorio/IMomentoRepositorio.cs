using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IMomentoRepositorio
{
    Task<Momento?> ObtenerPorId(int id, CancellationToken ct = default);
    Task<List<Momento>> ObtenerHijos(int id, CancellationToken ct = default);
    Task<int> Crear(Momento momento, CancellationToken ct = default);
    Task CambiarPadre(int id, int? padreId, CancellationToken ct = default);
    Task AgregarAlias(MomentoAlias alias, CancellationToken ct = default);
    Task<PublicacionMomento?> ObtenerAsociacion(int publicacionId, int momentoId, CancellationToken ct = default);
    Task<bool> GuardarAsociacion(PublicacionMomento asociacion, CancellationToken ct = default);
    Task<List<Momento>> ObtenerMomentosPublicacion(int publicacionId, CancellationToken ct = default);
}
