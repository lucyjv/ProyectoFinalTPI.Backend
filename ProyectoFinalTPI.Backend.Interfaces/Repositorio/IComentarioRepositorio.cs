using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IComentarioRepositorio
{
    Task<bool> ExistePublicacionVisible(int publicacionId, CancellationToken cancellationToken = default);
    Task<Comentario?> Obtener(int publicacionId, int comentarioId, CancellationToken cancellationToken = default);
    Task<Comentario?> ObtenerDetalle(int publicacionId, int comentarioId, CancellationToken cancellationToken = default);
    Task<List<Comentario>> ObtenerRaices(int publicacionId, CursorCronologicoDto? cursor, int limite,
        CancellationToken cancellationToken = default);
    Task<List<Comentario>> ObtenerRespuestas(int publicacionId, int comentarioPadreId, CursorCronologicoDto? cursor,
        int limite, CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> ContarRespuestasDirectas(IReadOnlyCollection<int> comentariosIds,
        CancellationToken cancellationToken = default);
    Task<int> Crear(Comentario comentario, CancellationToken cancellationToken = default);
    Task Eliminar(Comentario comentario, CancellationToken cancellationToken = default);
    Task EstablecerReaccion(int comentarioId, int usuarioId, TipoReaccionComentario tipo,
        CancellationToken cancellationToken = default);
    Task QuitarReaccion(int comentarioId, int usuarioId, CancellationToken cancellationToken = default);
    Task<Publicacion?> ObtenerPublicacionAsync(int publicacionId, CancellationToken cancellationToken = default);
    Task<int> ContarComentariosEnPosteosAjenosAsync(int usuarioId, CancellationToken cancellationToken = default);
    Task<int> ContarComentariosTotalesAsync(int usuarioId, CancellationToken cancellationToken = default);
}
