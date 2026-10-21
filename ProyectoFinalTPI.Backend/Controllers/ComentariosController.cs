using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Mappings;
using ProyectoFinalTPI.Backend.Requests.Comentarios;
using ProyectoFinalTPI.Backend.Extensions;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/publicaciones/{publicacionId:int}/comentarios")]
[SwaggerResponse(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al procesar la operación.", typeof(ProblemDetails))]
public class ComentariosController(IComentarioServicio comentarioServicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Obtiene los comentarios principales de una publicación.",
        Description = "Devuelve comentarios raíz paginados por cursor. El límite debe estar entre 1 y 50. Incluye hasta tres respuestas iniciales por comentario y cursores para continuar."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Página de comentarios principales.", typeof(ResultadoPaginadoDto<ComentarioDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El recurso solicitado no existe o no está disponible.", typeof(ProblemDetails))]
    public Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerRaices(
        int publicacionId, [FromQuery] string? cursor = null, [FromQuery] int limite = 20,
        CancellationToken cancellationToken = default) =>
        comentarioServicio.ObtenerRaices(
            publicacionId, cursor, limite, ObtenerUsuarioId(), cancellationToken);

    [HttpGet("{comentarioId:int}/respuestas")]
    [SwaggerOperation(
        Summary = "Obtiene las respuestas de un comentario.",
        Description = "Devuelve respuestas directas paginadas por cursor. El límite debe estar entre 1 y 20. La publicación y el comentario deben estar disponibles."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Página de respuestas.", typeof(ResultadoPaginadoDto<ComentarioDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El recurso solicitado no existe o no está disponible.", typeof(ProblemDetails))]
    public Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerRespuestas(
        int publicacionId, int comentarioId, [FromQuery] string? cursor = null, [FromQuery] int limite = 3,
        CancellationToken cancellationToken = default) =>
        comentarioServicio.ObtenerRespuestas(
            publicacionId, comentarioId, cursor, limite, ObtenerUsuarioId(), cancellationToken);

    [Authorize]
    [HttpPost]
    [SwaggerOperation(
        Summary = "Crea un comentario o una respuesta.",
        Description = "Requiere autenticación. Recibe contenido de hasta 1000 caracteres y un identificador opcional de comentario padre. El padre debe pertenecer a la publicación y no estar eliminado."
    )]
    [SwaggerResponse(StatusCodes.Status201Created, "Comentario creado. Devuelve su ubicación en Location.", typeof(ComentarioDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El recurso solicitado no existe o no está disponible.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "Formato de contenido no compatible; utilice application/json.", typeof(ProblemDetails))]
    public async Task<IActionResult> Crear(
        int publicacionId, [FromBody] CrearComentarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        var comentario = await comentarioServicio.Crear(
            request.ToComentario(publicacionId, usuarioId.Value), cancellationToken);
        return Created($"/api/publicaciones/{publicacionId}/comentarios/{comentario.Id}", comentario);
    }

    [Authorize]
    [HttpDelete("{comentarioId:int}")]
    [SwaggerOperation(
        Summary = "Elimina un comentario propio.",
        Description = "Requiere autenticación. Realiza una eliminación lógica y conserva las respuestas. Repetir la eliminación de un comentario propio ya eliminado no genera cambios."
    )]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Comentario eliminado; no devuelve contenido.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "El comentario pertenece a otro usuario.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El recurso solicitado no existe o no está disponible.", typeof(ProblemDetails))]
    public async Task<IActionResult> Eliminar(
        int publicacionId, int comentarioId, CancellationToken cancellationToken = default)
    {
        var usuarioId = ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        await comentarioServicio.Eliminar(publicacionId, comentarioId, usuarioId.Value, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPut("{comentarioId:int}/reaccion")]
    [SwaggerOperation(
        Summary = "Establece una reacción en un comentario.",
        Description = "Requiere autenticación. Recibe el tipo de reacción y devuelve el comentario con su estado actualizado. No permite reaccionar a comentarios eliminados."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Comentario con la reacción actualizada.", typeof(ComentarioDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El recurso solicitado no existe o no está disponible.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "Formato de contenido no compatible; utilice application/json.", typeof(ProblemDetails))]
    public async Task<ActionResult<ComentarioDto>> EstablecerReaccion(
        int publicacionId, int comentarioId, [FromBody] ReaccionComentarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        return Ok(await comentarioServicio.EstablecerReaccion(
            publicacionId, comentarioId, usuarioId.Value, request.Tipo, cancellationToken));
    }

    [Authorize]
    [HttpDelete("{comentarioId:int}/reaccion")]
    [SwaggerOperation(
        Summary = "Quita la reacción de un comentario.",
        Description = "Requiere autenticación. Quita la reacción del usuario y devuelve el comentario actualizado. No permite interactuar con comentarios eliminados."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Comentario con la reacción actualizada.", typeof(ComentarioDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El recurso solicitado no existe o no está disponible.", typeof(ProblemDetails))]
    public async Task<ActionResult<ComentarioDto>> QuitarReaccion(
        int publicacionId, int comentarioId, CancellationToken cancellationToken = default)
    {
        var usuarioId = ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        return Ok(await comentarioServicio.QuitarReaccion(
            publicacionId, comentarioId, usuarioId.Value, cancellationToken));
    }

    private int? ObtenerUsuarioId() => User.ObtenerUsuarioId();
}
