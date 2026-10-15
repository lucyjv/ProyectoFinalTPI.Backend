using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio
{
    public interface IPublicacionRepositorio
    {
        List<Publicacion> ObtenerTodasPublicaciones();
        List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria);

        Task<int> Crear(Publicacion publicacion);
        Task<int> CrearComentario(Comentario comentario, CancellationToken cancellationToken = default);
        Task<Publicacion?> ObtenerPorId(int id, CancellationToken cancellationToken = default);
        Task<List<PublicacionPinDto>> ObtenerPines(PublicacionFiltroDto filtro, int limite, CancellationToken cancellationToken = default);
        Task<PublicacionResumenDto?> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default);
        Task<PublicacionDetalleDto?> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerFeedSeguidos(IReadOnlyCollection<int> autoresIds, CursorCronologicoDto? cursor, int limite,
    CancellationToken cancellationToken = default);
        Task<List<ComentarioDto>> ObtenerComentarios(int publicacionId, CursorCronologicoDto? cursor, int limite, CancellationToken cancellationToken = default);
        Task<List<ComentarioDto>> ObtenerHilosComentarios(int publicacionId, CursorCronologicoDto? cursor, int limite, int? usuarioId,
            CancellationToken cancellationToken = default) => ObtenerComentarios(publicacionId, cursor, limite, cancellationToken);
        Task<Comentario?> ObtenerComentario(int publicacionId, int comentarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Comentario?>(null);
        Task<ComentarioDto?> ObtenerComentarioDto(int publicacionId, int comentarioId, int? usuarioId,
            CancellationToken cancellationToken = default) => Task.FromResult<ComentarioDto?>(null);
        Task EstablecerReaccionComentario(int comentarioId, int usuarioId, TipoReaccionComentario tipo,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        Task QuitarReaccionComentario(int comentarioId, int usuarioId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        Task<bool> ExistePublicacionVisible(int id, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(IReadOnlyCollection<CategoriaEnum> categorias, DateTime fechaCorte, CursorCronologicoDto? cursor,
        int limite, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerPublicacionesPorIds(IReadOnlyCollection<int> publicacionesIds, CancellationToken cancellationToken = default);
        Task<Dictionary<CategoriaEnum, int>> ObtenerCantidadPorCategoriaDelUsuario(int usuarioId, DateTime desde, CancellationToken cancellationToken = default);
        Task<bool> Eliminar(int id, int? autorId = null, CancellationToken cancellationToken = default);
        Task<bool> EliminarRepost(int originalId, int? autorId = null, CancellationToken cancellationToken = default);
    }
}
