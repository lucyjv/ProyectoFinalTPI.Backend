using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IPublicacionServicio
    {
        List<Publicacion> ObtenerPublicaciones();
        List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria);

        Task<int> Crear(CrearPublicacionDto dto);
        Task<ComentarioDto> CrearComentario(int publicacionId, CrearComentarioDto dto, CancellationToken cancellationToken = default);
        Task<int> Compartir(CompartirPublicacionDto dto, CancellationToken cancellationToken = default);
        Task<ResultadoMapaDto> ObtenerPines(PublicacionFiltroDto filtro, CancellationToken cancellationToken = default);
        Task<PublicacionResumenDto> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default);

        Task<PublicacionDetalleDto> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default);
        Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerComentarios(int publicacionId, string? cursor, int limite, 
            CancellationToken cancellationToken = default);
        Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerHilosComentarios(int publicacionId, string? cursor, int limite,
            int? usuarioId, CancellationToken cancellationToken = default) =>
            ObtenerComentarios(publicacionId, cursor, limite, cancellationToken);
        Task<ComentarioDto> EstablecerReaccionComentario(int publicacionId, int comentarioId, int usuarioId, string tipo,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        Task<ComentarioDto> QuitarReaccionComentario(int publicacionId, int comentarioId, int usuarioId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerFeedSeguidos(int usuarioId, string? cursor, int limite,
            CancellationToken cancellationToken = default);
        Task<ResultadoPaginadoDto<PublicacionDetalleDto>>ObtenerPublicacionesParaVos(int usuarioId, string? cursor, int limite,
            CancellationToken cancellationToken = default);
        Task Eliminar(int id, int? autorId = null, CancellationToken cancellationToken = default);
        Task EliminarRepost(int originalId, int? autorId = null, CancellationToken cancellationToken = default);
    }
}
