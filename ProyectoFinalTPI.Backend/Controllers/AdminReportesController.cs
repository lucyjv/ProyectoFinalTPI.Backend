using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize(Policy = "AdministrarPanel")]
[Route("api/admin/reportes")]
[SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
[SwaggerResponse(StatusCodes.Status403Forbidden, "Se requieren permisos de administrador.")]
public class AdminReportesController(IAdminReporteServicio servicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Lista los reportes.",
        Description = "Devuelve reportes paginados y filtrados para su revisión administrativa.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Página de reportes.", typeof(PaginaAdminDto<ReporteAdminDto>))]
    public Task<PaginaAdminDto<ReporteAdminDto>> Listar(
        [FromQuery] FiltroReportesAdminDto filtro, CancellationToken cancellationToken) =>
        servicio.Listar(filtro, cancellationToken);

    [HttpPatch]
    [SwaggerOperation(
        Summary = "Resuelve un reporte.",
        Description = "Aplica la decisión del administrador y resuelve los reportes pendientes del mismo contenido.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Reporte resuelto.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Acción inválida.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El reporte no existe.", typeof(ProblemDetails))]
    public async Task<IActionResult> Resolver(
        [FromBody] ResolverReporteAdminDto dto, CancellationToken cancellationToken)
    {
        await servicio.Resolver(
            User.ObtenerUsuarioIdRequerido(), dto, cancellationToken);
        return NoContent();
    }
}
