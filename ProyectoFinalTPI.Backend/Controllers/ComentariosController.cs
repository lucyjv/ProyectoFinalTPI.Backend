using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Mappings;
using ProyectoFinalTPI.Backend.Requests.Comentarios;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/publicaciones/{publicacionId:int}/comentarios")]
public class ComentariosController(IComentarioServicio comentarioServicio) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ResultadoPaginadoDto<ComentarioDto>), StatusCodes.Status200OK)]
    public Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerRaices(
        int publicacionId, [FromQuery] string? cursor = null, [FromQuery] int limite = 20,
        CancellationToken cancellationToken = default) =>
        comentarioServicio.ObtenerRaices(
            publicacionId, cursor, limite, ObtenerUsuarioId(), cancellationToken);

    [HttpGet("{comentarioId:int}/respuestas")]
    [ProducesResponseType(typeof(ResultadoPaginadoDto<ComentarioDto>), StatusCodes.Status200OK)]
    public Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerRespuestas(
        int publicacionId, int comentarioId, [FromQuery] string? cursor = null, [FromQuery] int limite = 3,
        CancellationToken cancellationToken = default) =>
        comentarioServicio.ObtenerRespuestas(
            publicacionId, comentarioId, cursor, limite, ObtenerUsuarioId(), cancellationToken);

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(ComentarioDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Crear(
        int publicacionId, [FromBody] CrearComentarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        var comentario = await comentarioServicio.Crear(
            publicacionId, request.ToCrearComentarioDto(usuarioId.Value), cancellationToken);
        return Created($"/api/publicaciones/{publicacionId}/comentarios/{comentario.Id}", comentario);
    }

    [Authorize]
    [HttpDelete("{comentarioId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Eliminar(
        int publicacionId, int comentarioId, CancellationToken cancellationToken = default)
    {
        var usuarioId = ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        await comentarioServicio.Eliminar(publicacionId, comentarioId, usuarioId.Value, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPut("{comentarioId:int}/reaccion")]
    [ProducesResponseType(typeof(ComentarioDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ComentarioDto>> EstablecerReaccion(
        int publicacionId, int comentarioId, [FromBody] ReaccionComentarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        return Ok(await comentarioServicio.EstablecerReaccion(
            publicacionId, comentarioId, usuarioId.Value, request.Tipo, cancellationToken));
    }

    [Authorize]
    [HttpDelete("{comentarioId:int}/reaccion")]
    [ProducesResponseType(typeof(ComentarioDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ComentarioDto>> QuitarReaccion(
        int publicacionId, int comentarioId, CancellationToken cancellationToken = default)
    {
        var usuarioId = ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        return Ok(await comentarioServicio.QuitarReaccion(
            publicacionId, comentarioId, usuarioId.Value, cancellationToken));
    }

    private int? ObtenerUsuarioId()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(valor, out var usuarioId) ? usuarioId : null;
    }
}
