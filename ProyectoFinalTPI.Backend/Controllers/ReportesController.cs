using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Reportes;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Mappings;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/reportes")]
[SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
public class ReportesController(IReporteServicio servicio) : ControllerBase
{
    [HttpPost]
    [SwaggerOperation(
        Summary = "Reporta contenido o un perfil.",
        Description = "Registra el reporte para revisión administrativa sin ocultar ni bloquear automáticamente.")]
    [SwaggerResponse(StatusCodes.Status201Created, "Reporte recibido.", typeof(ReporteCreadoDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Datos inválidos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El objetivo no existe.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Ya existe un reporte pendiente.", typeof(ProblemDetails))]
    public async Task<ActionResult<ReporteCreadoDto>> Crear(
        [FromBody] CrearReporteDto dto, CancellationToken cancellationToken)
    {
        var reporte = await servicio.Crear(
            User.ObtenerUsuarioIdRequerido(), dto, cancellationToken);
        return Created("/api/reportes", reporte.ACreadoDto());
    }
}
