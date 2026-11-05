using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize(Policy = "AdministrarPanel")]
[Route("api/admin/publicaciones")]
[SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
[SwaggerResponse(StatusCodes.Status403Forbidden, "Se requieren permisos de administrador.")]
public class AdminPublicacionesController(IAdminPublicacionServicio servicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Lista publicaciones para administración.", Description = "Devuelve publicaciones paginadas y permite filtrar por texto, autor, categoría y visibilidad.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Página de publicaciones.", typeof(PaginaAdminDto<PublicacionAdminDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Filtros inválidos.", typeof(ProblemDetails))]
    public Task<PaginaAdminDto<PublicacionAdminDto>> Listar(
        [FromQuery] FiltroPublicacionesAdminDto filtro, CancellationToken cancellationToken) =>
        servicio.Listar(filtro, cancellationToken);

    [HttpPut("{id:int}")]
    [SwaggerOperation(Summary = "Edita una publicación.", Description = "Actualiza título, descripción, fecha y categoría de una publicación.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Publicación actualizada.", typeof(PublicacionAdminDto))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación no existe.", typeof(ProblemDetails))]
    public async Task<ActionResult<PublicacionAdminDto>> Actualizar(
        int id, [FromBody] ActualizarPublicacionAdminDto dto, CancellationToken cancellationToken) =>
        Ok(await servicio.Actualizar(id, dto, cancellationToken));

    [HttpPatch("{id:int}")]
    [SwaggerOperation(Summary = "Oculta o restaura una publicación.", Description = "Al ocultar exige un motivo y genera una notificación administrativa para el autor.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Visibilidad actualizada.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación no existe.", typeof(ProblemDetails))]
    public async Task<IActionResult> CambiarVisibilidad(
        int id, [FromBody] CambiarVisibilidadPublicacionDto dto, CancellationToken cancellationToken)
    {
        await servicio.CambiarVisibilidad(id, dto, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [SwaggerOperation(Summary = "Elimina una publicación.", Description = "Elimina la publicación de PostgreSQL y sus interacciones de Neo4j.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Publicación eliminada.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación no existe.", typeof(ProblemDetails))]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken)
    {
        await servicio.Eliminar(id, cancellationToken);
        return NoContent();
    }
}
