using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.RankingMusica;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/ranking-musica")]
public sealed class RankingMusicaController(IRankingMusicaServicio servicio) : ControllerBase
{
    private bool EsAdministrador => User.HasClaim("esAdmin", "true");

    [AllowAnonymous]
    [HttpGet("actual")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [SwaggerOperation(
        Summary = "Consulta la ronda de música vigente.",
        Description = "Incluye las candidaturas ordenadas por década y puntaje. Si hay una sesión válida, incluye el cupo diario y el saldo restante. Saldo es null para visitantes o si su lectura falla por un error de base de datos o tiempo de espera; las canciones siguen disponibles.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Ronda vigente.", typeof(RankingMusicaActualDto))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No hay una ronda vigente.", typeof(ProblemDetails))]
    public async Task<ActionResult<RankingMusicaActualDto>> ObtenerActual(CancellationToken cancellationToken)
    {
        var ronda = await servicio.ObtenerActual(User.ObtenerUsuarioId(), cancellationToken)
            ?? throw new KeyNotFoundException("No hay una ronda de música vigente.");
        return Ok(ronda);
    }

    [AllowAnonymous]
    [HttpGet("resultados")]
    [SwaggerOperation(
        Summary = "Consulta los resultados de una ronda cerrada.",
        Description = "Devuelve las ganadoras y las clasificadas por década. Si se omite rondaId, se consulta la última ronda cerrada.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Resultados por década.", typeof(ResultadosRankingMusicaDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de ronda es inválido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La ronda cerrada solicitada no existe.", typeof(ProblemDetails))]
    public async Task<ActionResult<ResultadosRankingMusicaDto>> ObtenerResultados(
        [FromQuery] int? rondaId, CancellationToken cancellationToken)
    {
        var resultados = await servicio.ObtenerResultados(rondaId, cancellationToken)
            ?? throw new KeyNotFoundException("No se encontró una ronda cerrada con esa consulta.");
        return Ok(resultados);
    }

    [HttpPost("sugerencias")]
    [SwaggerOperation(
        Summary = "Sugiere una canción para la ronda vigente.",
        Description = "Requiere el permiso SugerirMusica de Mnemosine o Marca. La década se envía como su primer año, por ejemplo 1980.")]
    [SwaggerResponse(StatusCodes.Status201Created, "Candidatura creada.", typeof(CandidaturaRankingMusicaDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Título, artista o década inválidos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Se requiere el permiso SugerirMusica.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "La canción ya participa en esa década o la ronda no está vigente.", typeof(ProblemDetails))]
    public async Task<ActionResult<CandidaturaRankingMusicaDto>> SugerirMusica(
        [FromBody] CrearCandidaturaMusicaDto dto, CancellationToken cancellationToken)
    {
        var candidatura = await servicio.SugerirMusica(
            User.ObtenerUsuarioIdRequerido(), dto.Decada, dto.Titulo, dto.Artista, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, candidatura);
    }

    [HttpPost("puntuacion")]
    [SwaggerOperation(
        Summary = "Aporta puntos a una candidatura.",
        Description = "Requiere VotarCancion, disponible desde Nostálgico. Cada solicitud suma los puntos enviados y los descuenta del cupo compartido con publicaciones.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Voto registrado con el puntaje total y el saldo diario restante.", typeof(VotoRankingMusicaDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Candidatura o cantidad de puntos inválida.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Se requiere el permiso VotarCancion.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La candidatura no existe o fue retirada.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "La ronda no está vigente.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Puntos diarios insuficientes.", typeof(ProblemDetails))]
    public async Task<ActionResult<VotoRankingMusicaDto>> PuntuarMusica(
        [FromBody] PuntuarMusicaDto dto, CancellationToken cancellationToken) =>
        Ok(await servicio.PuntuarMusica(dto.CandidaturaId, User.ObtenerUsuarioIdRequerido(), dto.Puntos, cancellationToken));

    [HttpPut("{id:int}")]
    [SwaggerOperation(
        Summary = "Corrige el título o el artista de una candidatura.",
        Description = "Puede corregirla su creador o un administrador mientras la ronda esté vigente y la canción aún no tenga votos.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Candidatura corregida.", typeof(CandidaturaRankingMusicaDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Datos de la candidatura inválidos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Solo el creador o un administrador pueden corregirla.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La candidatura no existe o fue retirada.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "La candidatura recibió votos, la canción está duplicada o la ronda dejó de estar vigente.", typeof(ProblemDetails))]
    public async Task<ActionResult<CandidaturaRankingMusicaDto>> EditarCandidatura(
        [FromRoute] int id, [FromBody] DatosCandidaturaMusicaDto dto, CancellationToken cancellationToken) =>
        Ok(await servicio.EditarCandidatura(
            id, User.ObtenerUsuarioIdRequerido(), EsAdministrador, dto.Titulo, dto.Artista, cancellationToken));

    [HttpDelete("{id:int}")]
    [SwaggerOperation(
        Summary = "Retira una candidatura de la ronda vigente.",
        Description = "Puede retirarla su creador o un administrador. Los votos emitidos y el gasto diario se conservan.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Candidatura retirada.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de candidatura es inválido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Solo el creador o un administrador pueden retirarla.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "La candidatura no existe o fue retirada.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "La ronda no está vigente.", typeof(ProblemDetails))]
    public async Task<IActionResult> RetirarCandidatura([FromRoute] int id, CancellationToken cancellationToken)
    {
        await servicio.RetirarCandidatura(id, User.ObtenerUsuarioIdRequerido(), EsAdministrador, cancellationToken);
        return NoContent();
    }
}
