using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Servicio.Mappings;

internal static class ComentarioMapper
{
    public static ComentarioDto ToDto(
        this Comentario comentario,
        int? usuarioActualId,
        int cantidadRespuestas = 0,
        List<ComentarioDto>? respuestas = null,
        string? siguienteCursorRespuestas = null)
    {
        var eliminado = comentario.EstaEliminado;
        return new ComentarioDto
        {
            Id = comentario.Id,
            PublicacionId = comentario.PublicacionId,
            ComentarioPadreId = comentario.ComentarioPadreId,
            Contenido = eliminado ? null : comentario.Contenido,
            FechaCreacion = comentario.FechaCreacion,
            Autor = eliminado ? null : new AutorResumenDto
            {
                Id = comentario.UsuarioId,
                Username = comentario.Usuario.Username
            },
            EstaEliminado = eliminado,
            PuedeEliminar = !eliminado && usuarioActualId == comentario.UsuarioId,
            CantidadRespuestas = cantidadRespuestas,
            SiguienteCursorRespuestas = siguienteCursorRespuestas,
            Reacciones = eliminado
                ? new Dictionary<string, int>()
                : comentario.Reacciones
                    .GroupBy(reaccion => reaccion.Tipo.ToString().ToLowerInvariant())
                    .ToDictionary(grupo => grupo.Key, grupo => grupo.Count()),
            ReaccionActual = eliminado || !usuarioActualId.HasValue
                ? null
                : comentario.Reacciones
                    .FirstOrDefault(reaccion => reaccion.UsuarioId == usuarioActualId.Value)?
                    .Tipo.ToString().ToLowerInvariant(),
            Respuestas = respuestas ?? new List<ComentarioDto>()
        };
    }
}
