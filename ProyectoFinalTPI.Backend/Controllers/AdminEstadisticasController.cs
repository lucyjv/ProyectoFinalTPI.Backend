using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize(Policy = "AdministrarPanel")]
[Route("api/admin/estadisticas")]
[SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
[SwaggerResponse(StatusCodes.Status403Forbidden, "Se requieren permisos de administrador.")]
public class AdminEstadisticasController(IAdminEstadisticaServicio servicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Obtiene estadísticas administrativas.", Description = "Combina totales de PostgreSQL con rankings de likes y puntos de nostalgia de Neo4j.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Estadísticas del panel.", typeof(EstadisticasAdminDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El límite no es válido.", typeof(ProblemDetails))]
    public Task<EstadisticasAdminDto> Obtener(
        [FromQuery] int limiteRanking = 10, CancellationToken cancellationToken = default) =>
        servicio.Obtener(limiteRanking, cancellationToken);
}
