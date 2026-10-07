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
        Task<List<PublicacionPinDto>> ObtenerPines(PublicacionFiltroDto filtro, int limite, CancellationToken cancellationToken = default);
        Task<PublicacionResumenDto?> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default);
        Task<PublicacionDetalleDto?> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerFeedSeguidos(IReadOnlyCollection<int> autoresIds, CursorCronologicoDto? cursor, int limite,
    CancellationToken cancellationToken = default);
        Task<List<ComentarioDto>> ObtenerComentarios(int publicacionId, CursorCronologicoDto? cursor, int limite, CancellationToken cancellationToken = default);
        Task<bool> ExistePublicacionVisible(int id, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(IReadOnlyCollection<CategoriaEnum> categorias, DateTime fechaCorte, CursorCronologicoDto? cursor,
        int limite, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerPublicacionesPorIds(IReadOnlyCollection<int> publicacionesIds, CancellationToken cancellationToken = default);
        Task<Dictionary<CategoriaEnum, int>> ObtenerCantidadPorCategoriaDelUsuario(int usuarioId, DateTime desde, CancellationToken cancellationToken = default);
    }
}
