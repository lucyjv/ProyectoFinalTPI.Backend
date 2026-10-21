using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio
{
    public interface IPublicacionRepositorio
    {
        List<Publicacion> ObtenerTodasPublicaciones();
        List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria);

        Task<int> Crear(Publicacion publicacion);
        Task<Publicacion?> ObtenerPorId(int id, CancellationToken cancellationToken = default);
        Task<List<PublicacionPinDto>> ObtenerPines(PublicacionFiltroDto filtro, int limite, CancellationToken cancellationToken = default);
        Task<PublicacionResumenDto?> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default);
        Task<PublicacionDetalleDto?> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerFeedSeguidos(IReadOnlyCollection<int> autoresIds, CursorCronologicoDto? cursor, int limite,
    CancellationToken cancellationToken = default);
        Task<bool> ExistePublicacionVisible(int id, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(IReadOnlyCollection<CategoriaEnum> categorias, DateTime fechaCorte, CursorCronologicoDto? cursor,
        int limite, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerPublicacionesPorIds(IReadOnlyCollection<int> publicacionesIds, CancellationToken cancellationToken = default);
        Task<List<PublicacionDetalleDto>> ObtenerPorUsuarioId(int usuarioId, CancellationToken cancellationToken = default);
        Task CrearLike(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
        Task QuitarLike(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
        Task<MeGustaPublicacionDto> ObtenerEstadoLike(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<int, (int Cantidad, bool DioLike)>> ObtenerResumenLikes(
            IReadOnlyCollection<int> publicacionesIds, int usuarioId, CancellationToken cancellationToken = default);
        Task PuntuarPublicacion(int publicacionId, int usuarioId, int puntaje, CancellationToken cancellationToken = default);
        Task QuitarPuntuacion(int publicacionId, int usuarioId, CancellationToken cancellationToken = default);
        Task<PuntuacionPublicacionDto> ObtenerEstadoPuntuacion(int publicacionId, int? usuarioId, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<int, (int PuntosTotales, int? MiPuntuacion)>> ObtenerResumenPuntuaciones(
            IReadOnlyCollection<int> publicacionesIds, int? usuarioId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<int>> ObtenerPublicacionesRelacionadasPorLikes(
            int usuarioId, int cantidadMaxima, CancellationToken cancellationToken = default);
        Task<Dictionary<CategoriaEnum, int>> ObtenerCantidadPorCategoriaDelUsuario(int usuarioId, DateTime desde, CancellationToken cancellationToken = default);
        Task<int> ContarPosteosCercanosAlUsuario(int usuarioId, NetTopologySuite.Geometries.Point gpsUsuario);
        Task<int> ContarPosteosDeDiferentesEpocasDelUsuario(int usuarioId);
        Task<bool> Eliminar(int id, int? autorId = null, CancellationToken cancellationToken = default);
        Task<bool> EliminarRepost(int originalId, int? autorId = null, CancellationToken cancellationToken = default);
    }
}
