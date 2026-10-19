using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Requests.Comentarios;

namespace ProyectoFinalTPI.Backend.Mappings;

public static class ComentarioMappings
{
    public static Comentario ToComentario(
        this CrearComentarioRequest request,
        int publicacionId, int usuarioId)
    {
        return new Comentario
        {
            PublicacionId = publicacionId,
            Contenido = request.Contenido,
            UsuarioId = usuarioId,
            ComentarioPadreId = request.ComentarioPadreId
        };
    }
}
