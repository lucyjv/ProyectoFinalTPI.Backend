using ProyectoFinalTPI.Backend.Dtos.Publicacion;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IMeGustaPublicacionServicio
{
    Task<MeGustaPublicacionDto> DarLike(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
    Task<MeGustaPublicacionDto> QuitarLike(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
    Task CompletarResumenes(
        IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int usuarioId,
        CancellationToken cancellationToken = default);
}
