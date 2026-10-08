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
        Task<int> Compartir(CompartirPublicacionDto dto, CancellationToken cancellationToken = default);
        Task<ResultadoMapaDto> ObtenerPines(PublicacionFiltroDto filtro, CancellationToken cancellationToken = default);
        Task<PublicacionResumenDto> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default);

        Task<PublicacionDetalleDto> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default);
        Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerComentarios(int publicacionId, string? cursor, int limite, 
            CancellationToken cancellationToken = default);
        Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerFeedSeguidos(IReadOnlyCollection<int> autoresIds, string? cursor, int limite,
            CancellationToken cancellationToken = default);
        Task<ResultadoPaginadoDto<PublicacionDetalleDto>>ObtenerPublicacionesParaVos(int usuarioId, string? cursor, int limite,
            CancellationToken cancellationToken = default);
    }
}
