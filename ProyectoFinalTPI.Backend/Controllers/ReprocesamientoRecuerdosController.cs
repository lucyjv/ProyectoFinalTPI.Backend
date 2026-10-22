using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize(Policy = "AdministrarAnalisis")]
[Route("api/admin/analisis-recuerdos/reprocesamiento")]
public class ReprocesamientoRecuerdosController(IReprocesamientoRecuerdosServicio servicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Previsualiza recuerdos elegibles para reprocesar.",
        Description = "Requiere una sesión de administrador con esAdmin=true. Lista originales visibles con automatización permitida y sin trabajo de la versión actual o terminados en sinAnalizador; incluirFallidos=true agrega fallidos. No encola ni llama al proveedor. limite admite de 1 a 25 (10 por defecto). Pagina por Id ascendente usando siguienteDespuesDeId como despuesDeId. analisisDisponible indica si el proveedor real y el worker están habilitados."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Vista previa de candidatos, paginación y disponibilidad del análisis.", typeof(VistaPreviaReprocesamientoDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El límite o el identificador de continuación no son válidos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una sesión válida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Se requiere una sesión de administrador.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudo consultar la vista previa.", typeof(ProblemDetails))]
    public async Task<ActionResult<VistaPreviaReprocesamientoDto>> Previsualizar(CancellationToken ct,
        [FromQuery] int limite = 10, [FromQuery] int despuesDeId = 0, [FromQuery] bool incluirFallidos = false)
        => Ok(await servicio.Previsualizar(limite, despuesDeId, incluirFallidos, ct));

    [HttpPost]
    [SwaggerOperation(
        Summary = "Programa un lote de recuerdos existentes para su análisis.",
        Description = "Requiere administrador y proveedor real y worker habilitados. Recibe de 1 a 25 recuerdos distintos con publicacionId y versionContenido; intervaloSegundos admite de 10 a 3600 (10 por defecto). Revalida cada selección, crea trabajos faltantes y reinicia los sinAnalizador; los fallidos requieren incluirFallidos=true. Devuelve encolado, reprogramado u omitido con motivo por elemento. No reinicia trabajos pendientes, procesando o ya analizados ni modifica decisiones del autor. El 202 confirma el encolado, no la finalización; puede devolver programados=0. Consultar después el estado de análisis de cada publicación."
    )]
    [SwaggerResponse(StatusCodes.Status202Accepted, "Resultado del lote con trabajos programados, horarios y motivos de omisión.", typeof(ResultadoReprocesamientoDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "La selección, los identificadores, las versiones o el intervalo no son válidos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una sesión válida con identificador de usuario.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Se requiere una sesión de administrador.")]
    [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "El formato del cuerpo no es compatible; utilice application/json.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status503ServiceUnavailable, "El proveedor real o el worker están deshabilitados; no se encoló el lote.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudo confirmar el lote; los cambios de encolado se revierten.", typeof(ProblemDetails))]
    public async Task<ActionResult<ResultadoReprocesamientoDto>> Encolar([FromBody] SolicitudReprocesamientoDto solicitud, CancellationToken ct)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (usuarioId is null or <= 0) return Unauthorized();
        return Accepted(await servicio.Encolar(solicitud, usuarioId.Value, ct));
    }
}
