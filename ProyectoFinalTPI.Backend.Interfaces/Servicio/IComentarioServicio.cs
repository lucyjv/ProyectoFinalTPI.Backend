using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IComentarioServicio
{
    Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerRaices(int publicacionId, string? cursor, int limite,
        int? usuarioId, CancellationToken cancellationToken = default);
    Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerRespuestas(int publicacionId, int comentarioId, string? cursor,
        int limite, int? usuarioId, CancellationToken cancellationToken = default);
    Task<ComentarioDto> Crear(int publicacionId, CrearComentarioDto dto,
        CancellationToken cancellationToken = default);
    Task Eliminar(int publicacionId, int comentarioId, int usuarioId,
        CancellationToken cancellationToken = default);
    Task<ComentarioDto> EstablecerReaccion(int publicacionId, int comentarioId, int usuarioId, string tipo,
        CancellationToken cancellationToken = default);
    Task<ComentarioDto> QuitarReaccion(int publicacionId, int comentarioId, int usuarioId,
        CancellationToken cancellationToken = default);
}
