using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

// Operaciones internas de creación/asociación. Los endpoints usan IExploracionMomentosServicio.
public interface IMomentoServicio
{
    Task<int> Crear(Momento momento, CancellationToken ct = default);
    Task CambiarPadre(int id, int? padreId, CancellationToken ct = default);
    Task AgregarAlias(int id, string texto, CancellationToken ct = default);
    Task<bool> AsociarAutomaticamente(int publicacionId, int momentoId, double confianza, string versionAnalisis, CancellationToken ct = default);
    Task AsociarPorAutor(int publicacionId, int momentoId, int usuarioId, CancellationToken ct = default);
    Task<List<Momento>> ObtenerMomentosPublicacion(int publicacionId, CancellationToken ct = default);
}
