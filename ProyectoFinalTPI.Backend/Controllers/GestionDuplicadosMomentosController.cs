using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize(Policy = "AdministrarAnalisis")]
[Route("api/admin/momentos/{origenId:int}")]
public class GestionDuplicadosMomentosController(IGestionDuplicadosMomentoServicio servicio) : ControllerBase
{
    [HttpGet("duplicados")]
    [SwaggerOperation(Summary = "Busca posibles duplicados de un momento.",
        Description = "Requiere administrador. Compara nombres y alias normalizados entre momentos con períodos compatibles. limite admite de 1 a 50 (20 por defecto). El puntaje es una sugerencia textual, no confirma equivalencia semántica ni genera una fusión. No llama a Gemini. Los IDs fusionados se resuelven al canónico.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Candidatos ordenados por similitud; puede estar vacío.", typeof(IReadOnlyList<CandidatoDuplicadoMomentoDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador o el límite no son válidos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una sesión válida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Se requiere administrador.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El momento no existe.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "El catálogo contiene redirecciones inconsistentes.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudieron buscar los duplicados.", typeof(ProblemDetails))]
    public async Task<ActionResult<IReadOnlyList<CandidatoDuplicadoMomentoDto>>> Buscar(int origenId, CancellationToken ct, [FromQuery] int limite = 20)
        => Ok(await servicio.BuscarCandidatos(origenId, limite, ct));

    [HttpGet("fusion")]
    [SwaggerOperation(Summary = "Previsualiza la fusión de dos momentos.",
        Description = "Requiere administrador. Recibe destinoId por query y muestra identidades, versiones de catálogo, publicaciones afectadas, conflictos entre decisiones del autor e hijos a trasladar. No modifica datos. No admite períodos incompatibles ni relaciones de ancestro y descendiente. El destino conserva sus datos y jerarquía.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Vista previa para revisar antes de confirmar.", typeof(VistaFusionMomentoDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Seleccionar dos identificadores positivos y distintos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una sesión válida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Se requiere administrador.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Alguno de los momentos no existe.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Los períodos, la jerarquía o las redirecciones no permiten fusionar.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudo obtener la vista previa.", typeof(ProblemDetails))]
    public async Task<ActionResult<VistaFusionMomentoDto>> Previsualizar(int origenId, [FromQuery] int destinoId, CancellationToken ct)
        => Ok(await servicio.Previsualizar(origenId, destinoId, ct));

    [HttpPost("fusion")]
    [SwaggerOperation(Summary = "Fusiona un momento duplicado en el momento canónico elegido.",
        Description = "Requiere administrador. Recibe destinoId, versionOrigen y versionDestino de la vista previa. Traslada publicaciones e hijos, conserva alias y deja una redirección permanente para el Id anterior. En conflictos, prevalece el rechazo del autor, luego su aceptación y después el vínculo automático de mayor confianza. Incrementa versionRelaciones de cada publicación afectada; las correcciones antiguas deben recargarse. La operación es transaccional. Repetir una fusión ya realizada hacia el mismo canónico no duplica cambios. No se puede deshacer desde este endpoint.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Fusión confirmada o ya realizada, con el identificador canónico y cantidades afectadas.", typeof(ResultadoFusionMomentoDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los identificadores o las versiones no son válidos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una sesión válida con identificador de usuario.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Se requiere administrador.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Alguno de los momentos no existe.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "El catálogo cambió o los períodos, la jerarquía o las redirecciones no permiten fusionar. Volver a previsualizar.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "Utilice application/json.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudo confirmar la fusión; sus cambios se revierten.", typeof(ProblemDetails))]
    public async Task<ActionResult<ResultadoFusionMomentoDto>> Fusionar(int origenId, [FromBody] SolicitudFusionMomentoDto solicitud, CancellationToken ct)
    {
        var administradorId = User.ObtenerUsuarioId();
        if (administradorId is null or <= 0) return Unauthorized();
        return Ok(await servicio.Fusionar(origenId, solicitud, administradorId.Value, ct));
    }
}
