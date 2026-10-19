using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Extensions;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
[SwaggerResponse(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al procesar la operación.", typeof(ProblemDetails))]
public class ChatController : ControllerBase
{
    private readonly IChatServicio _chatServicio;

    public ChatController(IChatServicio chatServicio)
    {
        _chatServicio = chatServicio;
    }

    [HttpGet("conversaciones")]
    [SwaggerOperation(
        Summary = "Obtiene las conversaciones activas.",
        Description = "Requiere autenticación. Devuelve una entrada por cada usuario con quien se intercambiaron mensajes que aún no expiraron, junto con el último mensaje. Los mensajes vencen a los siete días."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Conversaciones activas; la lista puede estar vacía.", typeof(IEnumerable<ProyectoFinalTPI.Backend.Dtos.Chat.ConversacionResumenDto>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "El token no permite identificar un usuario válido.", typeof(ProblemDetails))]
    public async Task<IActionResult> ObtenerConversaciones(CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioIdAutenticado();
        var conversaciones = await _chatServicio.ObtenerConversacionesActivasAsync(usuarioId);
        return Ok(conversaciones);
    }

    private int ObtenerUsuarioIdAutenticado() => User.ObtenerUsuarioIdRequerido();
}
