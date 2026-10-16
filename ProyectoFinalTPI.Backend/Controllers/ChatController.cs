using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatServicio _chatServicio;

    public ChatController(IChatServicio chatServicio)
    {
        _chatServicio = chatServicio;
    }

    /// <summary>
    /// Devuelve las conversaciones activas (con mensajes no expirados) del usuario autenticado.
    /// Una entrada por cada usuario con quien haya intercambiado mensajes en los últimos 7 días.
    /// </summary>
    /// <response code="200">Lista de conversaciones activas (puede ser vacía).</response>
    /// <response code="401">El usuario no está autenticado.</response>
    [HttpGet("conversaciones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObtenerConversaciones(CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioIdAutenticado();
        var conversaciones = await _chatServicio.ObtenerConversacionesActivasAsync(usuarioId);
        return Ok(conversaciones);
    }

    private int ObtenerUsuarioIdAutenticado()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(valor!);
    }
}
