using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Dtos.Usuario;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IEtiquetadoPublicacionServicio
    {
        Task<int> EtiquetarUsuarios(
            int publicacionId,
            IReadOnlyCollection<int> usuariosIds,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<AutorResumenDto>> ObtenerUsuariosEtiquetados(
            int publicacionId,
            CancellationToken cancellationToken = default);

        Task<bool> DesetiquetarUsuario(
            int publicacionId,
            int usuarioId,
            CancellationToken cancellationToken = default);

        Task CompletarEtiquetas(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones,
            CancellationToken cancellationToken = default);

        Task<List<PublicacionDetalleDto>> ObtenerPublicacionesDondeEstaEtiquetado(
            int usuarioId,
            CancellationToken cancellationToken = default);
    }
}
