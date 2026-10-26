using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IPublicacionServicio
    {
        List<Publicacion> ObtenerPublicaciones();
        List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria);

        Task<int> Crear(Publicacion publicacion, Stream? archivo = null, string? nombreArchivo = null, Point? gpsUsuario = null);
        Task<ResultadoMapaDto> ObtenerPines(PublicacionFiltroDto filtro, CancellationToken cancellationToken = default);
        Task<PublicacionResumenDto> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default);

        Task<PublicacionDetalleDto> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerPorUsuarioId(int usuarioId, int? usuarioAutenticadoId, CancellationToken cancellationToken = default);
        Task<MeGustaPublicacionDto> DarLike(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
        Task<MeGustaPublicacionDto> QuitarLike(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
        Task CompletarResumenesLikes(IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int usuarioId,
            CancellationToken cancellationToken = default);
        Task<PuntuacionPublicacionDto> Puntuar(int publicacionId, int usuarioId, int puntaje, CancellationToken cancellationToken = default);
        Task<PuntuacionPublicacionDto> QuitarPuntuacion(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
        Task CompletarResumenesPuntuaciones(IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int? usuarioId,
            CancellationToken cancellationToken = default);
        Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerFeedSeguidos(int usuarioId, string? cursor, int limite,
            CancellationToken cancellationToken = default);
        Task<ResultadoPaginadoDto<PublicacionDetalleDto>>ObtenerPublicacionesParaVos(int usuarioId, string? cursor, int limite,
            CancellationToken cancellationToken = default);
        Task RegistrarImpresionesFeed(int usuarioId, IReadOnlyCollection<int> publicacionesIds,
            CancellationToken cancellationToken = default);
        Task Eliminar(int id, int? autorId = null, CancellationToken cancellationToken = default);
        Task<RepostPublicacionDto> Repostear(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
        Task<RepostPublicacionDto> QuitarRepost(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
        Task<int> Citar(int publicacionId, int usuarioId, string descripcion, CancellationToken cancellationToken = default);
        Task CompletarResumenesReposts(IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int usuarioId,
            CancellationToken cancellationToken = default);
    }
}
