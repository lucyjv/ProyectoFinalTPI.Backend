using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/publicaciones/{id:int}/analisis")]
public class AnalisisRecuerdosController(IAnalisisRecuerdosServicio servicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Consulta el estado del análisis de un recuerdo.",
        Description = "Devuelve publicacionId, versionContenido y estado de la versión actual. Estados: noProgramado, pendiente, procesando, completado, sinMomento, sinAnalizador, descartado, fallido y desactivado. No inicia análisis ni expone errores internos, intentos o reservas. Resuelve reposts al original visible. Consultar de forma espaciada mientras esté pendiente o procesando y recargar los momentos al finalizar. No requiere autenticación."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Estado público del análisis de la versión actual del recuerdo.", typeof(EstadoAnalisisRecuerdoDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación no es válido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación o su original no existen o no están disponibles.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudo consultar el estado del análisis.", typeof(ProblemDetails))]
    public async Task<ActionResult<EstadoAnalisisRecuerdoDto>> Obtener(int id, CancellationToken ct)
        => Ok(await servicio.ObtenerEstado(id, ct));
}
