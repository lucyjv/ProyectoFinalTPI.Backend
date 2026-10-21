using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/momentos")]
public class MomentosController(IExploracionMomentosServicio servicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Busca acontecimientos colectivos por nombre o alias.",
        Description = "Sin busqueda, lista el catálogo completo por Id ascendente. La búsqueda admite hasta 200 caracteres. Pagina con limite de 1 a 50 (20 por defecto) y cursor de continuación; al continuar, conservar la búsqueda original. No requiere autenticación."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Página de momentos encontrados; puede estar vacía.", typeof(ResultadoPaginadoDto<MomentoResumenDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "La búsqueda, el límite o el cursor no son válidos, o el cursor pertenece a otra búsqueda.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudieron buscar los momentos.", typeof(ProblemDetails))]
    public async Task<ActionResult<ResultadoPaginadoDto<MomentoResumenDto>>> Buscar(
        [FromQuery] string? busqueda = null, [FromQuery] string? cursor = null, [FromQuery] int limite = 20, CancellationToken ct = default)
        => Ok(await servicio.Buscar(busqueda, cursor, limite, ct));

    [HttpGet("{id:int}")]
    [SwaggerOperation(
        Summary = "Obtiene el detalle de un momento.",
        Description = "Devuelve el acontecimiento colectivo con descripción, fechas, padre, hijos directos, alias y versionCatalogo. Si el Id fue fusionado, devuelve 200 con el canónico y redirigidoDesdeId; no es una redirección HTTP. No requiere autenticación."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Detalle del momento solicitado.", typeof(MomentoDetalleDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador del momento no es válido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El momento no existe.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudo obtener el momento.", typeof(ProblemDetails))]
    public async Task<ActionResult<MomentoDetalleDto>> Obtener(int id, CancellationToken ct)
        => Ok(await servicio.ObtenerDetalle(id, ct));

    [HttpGet("{id:int}/publicaciones")]
    [SwaggerOperation(
        Summary = "Lista los recuerdos asociados a un momento.",
        Description = "Devuelve publicaciones originales visibles con asociaciones activas, sin duplicados, ordenadas por fecha del recuerdo e Id descendentes. Resuelve IDs fusionados al momento canónico. incluirDescendientes=true (predeterminado) incorpora su jerarquía. Pagina con limite de 1 a 50 (20 por defecto); conservar el Id consultado y la opción de descendientes al continuar con siguienteCursor. No requiere autenticación."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Página de publicaciones asociadas al momento.", typeof(ResultadoPaginadoDto<PublicacionResumenDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador, el límite o el cursor no son válidos, o el cursor corresponde a otra consulta.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El momento no existe.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudieron listar las publicaciones del momento.", typeof(ProblemDetails))]
    public async Task<ActionResult<ResultadoPaginadoDto<PublicacionResumenDto>>> Publicaciones(
        int id, [FromQuery] bool incluirDescendientes = true, [FromQuery] string? cursor = null,
        [FromQuery] int limite = 20, CancellationToken ct = default)
        => Ok(await servicio.ObtenerPublicaciones(id, incluirDescendientes, cursor, limite, ct));
}
