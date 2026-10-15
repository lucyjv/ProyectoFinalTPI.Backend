using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;

namespace ProyectoFinalTPI.Backend.Mappings;

public static class ComentarioMappings
{
    public static CrearComentarioDto ToCrearComentarioDto(this CrearComentarioRequest request)
    {
        return new CrearComentarioDto
        {
            Contenido = request.Contenido,
            UsuarioId = request.UsuarioId
        };
    }
}
