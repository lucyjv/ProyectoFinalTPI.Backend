using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/publicaciones/{id:int}/momentos")]
public class MomentosPublicacionController(IExploracionMomentosServicio servicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Obtiene los momentos asociados a una publicación.",
        Description = "Devuelve asociaciones activas, su origen, la preferencia de automatización y versionRelaciones para futuras correcciones. Resuelve los reposts al original visible. La consulta es pública; una sesión permite indicar si el usuario puede corregir la selección mediante puedeCorregir."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Momentos activos y versión de relaciones de la publicación.", typeof(MomentosPublicacionDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación no es válido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación o su original no existen o no están disponibles.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudieron obtener los momentos de la publicación.", typeof(ProblemDetails))]
    public async Task<ActionResult<MomentosPublicacionDto>> Obtener(int id, CancellationToken ct)
        => Ok(await servicio.ObtenerEstado(id, User.ObtenerUsuarioId(), ct));

    [Authorize]
    [HttpPut]
    [SwaggerOperation(
        Summary = "Corrige los momentos de una publicación propia.",
        Description = "Requiere autenticación y ser autor del recuerdo original; no permite corregir un repost. Recibe la selección completa de hasta 50 momentoIds distintos, permitirMomentosAutomaticos y la versionRelaciones obtenida al consultar. Los vínculos omitidos se rechazan y el análisis automático respeta esas decisiones. Una lista vacía es válida. Ante 409, recargar la selección antes de volver a corregir."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Selección actualizada y nueva versión de relaciones, si hubo cambios.", typeof(MomentosPublicacionDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador, la selección o la versión no son válidos, o se intentó corregir un repost.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una sesión válida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Solo el autor de la publicación puede corregir sus momentos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación o alguno de los momentos seleccionados no existen o no están disponibles.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Las relaciones cambiaron desde la consulta; recargar su versión y selección.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "El formato del cuerpo no es compatible; utilice application/json.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudo guardar la corrección de momentos.", typeof(ProblemDetails))]
    public async Task<ActionResult<MomentosPublicacionDto>> Corregir(int id, [FromBody] CorregirMomentosRequest request, CancellationToken ct)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (usuarioId is null or <= 0) return Unauthorized();
        return Ok(await servicio.Corregir(id, usuarioId.Value, request.MomentoIds!, request.PermitirMomentosAutomaticos!.Value, request.VersionRelaciones!.Value, ct));
    }
}
