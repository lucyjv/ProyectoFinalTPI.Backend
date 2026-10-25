using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/publicaciones/{id:int}")]
public class RelacionesRecuerdosController(IRelacionRecuerdosServicio servicio) : ControllerBase
{
    [HttpGet("relaciones")]
    [SwaggerOperation(
        Summary = "Obtiene las relaciones de un recuerdo por día y lugar.",
        Description = "Devuelve hasta seis recuerdos por bloque dentro de un radio inclusivo de 200 metros. El bloque día exige además el mismo día, mes y año en UTC. Resuelve los reposts al original visible y excluye recuerdos ocultos, reposts y la publicación consultada. No requiere autenticación."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Relaciones del recuerdo agrupadas por día y lugar.", typeof(RelacionesRecuerdoDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación no es válido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación o su original no existen o no están disponibles.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudieron obtener las relaciones.", typeof(ProblemDetails))]
    public async Task<ActionResult<RelacionesRecuerdoDto>> Obtener(int id, CancellationToken ct)
        => Ok(await servicio.ObtenerRelaciones(id, ct));

    [HttpGet("relacionadas")]
    [SwaggerOperation(
        Summary = "Lista los recuerdos relacionados por día o lugar.",
        Description = "Acepta tipo=lugar (predeterminado) o tipo=dia. Ambos incluyen recuerdos a 200 metros o menos; dia exige también la misma fecha completa en UTC. Pagina por distancia e Id ascendentes, con limite de 1 a 50 (20 por defecto). Para continuar, enviar siguienteCursor conservando el tipo y la publicación de origen. Resuelve reposts al original visible. No requiere autenticación."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Página de recuerdos relacionados y cursor de continuación, si corresponde.", typeof(ResultadoPaginadoDto<RecuerdoRelacionadoDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador, tipo, límite o cursor no son válidos, o el cursor corresponde a otra consulta.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación o su original no existen o no están disponibles.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudieron listar los recuerdos relacionados.", typeof(ProblemDetails))]
    public async Task<ActionResult<ResultadoPaginadoDto<RecuerdoRelacionadoDto>>> Listar(
        int id, [FromQuery] string tipo = "lugar", [FromQuery] string? cursor = null,
        [FromQuery] int limite = 20, CancellationToken ct = default)
        => Ok(await servicio.ObtenerRelacionadas(id, tipo, cursor, limite, ct));
}
