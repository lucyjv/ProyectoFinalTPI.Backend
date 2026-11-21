using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.RankingMusica;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize(Policy = "AdministrarPanel")]
[Route("api/admin/ranking-musica")]
public sealed class AdminRankingMusicaController(IRankingMusicaServicio servicio) : ControllerBase
{
    [HttpPost]
    [SwaggerOperation(
        Summary = "Agrega una canción desde el panel administrativo.",
        Description = "La canción participa en la ronda vigente. La década se envía como su primer año, por ejemplo 1980.")]
    [SwaggerResponse(StatusCodes.Status201Created, "Candidatura creada.", typeof(CandidaturaRankingMusicaDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Título, artista o década inválidos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Se requieren permisos de administrador.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "La canción ya participa en esa década o la ronda no está vigente.", typeof(ProblemDetails))]
    public async Task<ActionResult<CandidaturaRankingMusicaDto>> AgregarCandidatura(
        [FromBody] CrearCandidaturaMusicaDto dto, CancellationToken cancellationToken)
    {
        var candidatura = await servicio.AgregarMusicaComoAdministrador(
            User.ObtenerUsuarioIdRequerido(), true, dto.Decada, dto.Titulo, dto.Artista, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, candidatura);
    }
}
