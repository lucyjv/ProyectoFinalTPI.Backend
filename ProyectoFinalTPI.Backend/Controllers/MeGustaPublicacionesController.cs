using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/publicaciones/{publicacionId:int}/like")]
public class MeGustaPublicacionesController(IMeGustaPublicacionServicio meGustaServicio) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(MeGustaPublicacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeGustaPublicacionDto>> DarLike(
        int publicacionId, CancellationToken cancellationToken = default)
    {
        if (!TryObtenerUsuarioId(out var usuarioId)) return Unauthorized();
        return Ok(await meGustaServicio.DarLike(publicacionId, usuarioId, cancellationToken));
    }

    [HttpDelete]
    [ProducesResponseType(typeof(MeGustaPublicacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeGustaPublicacionDto>> QuitarLike(
        int publicacionId, CancellationToken cancellationToken = default)
    {
        if (!TryObtenerUsuarioId(out var usuarioId)) return Unauthorized();
        return Ok(await meGustaServicio.QuitarLike(publicacionId, usuarioId, cancellationToken));
    }

    private bool TryObtenerUsuarioId(out int usuarioId)
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(valor, out usuarioId) && usuarioId > 0;
    }
}
