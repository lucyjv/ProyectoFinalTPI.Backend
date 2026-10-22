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

    [HttpDelete("conversaciones/{otroUsuarioId:int}")]
    [SwaggerOperation(
        Summary = "Elimina la conversación con un usuario específico.",
        Description = "Requiere autenticación. Elimina todos los mensajes entre el usuario autenticado y el usuario indicado, independientemente de su fecha de expiración."
    )]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Conversación eliminada correctamente.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El id del otro usuario no puede ser el mismo que el del usuario autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "El token no permite identificar un usuario válido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No existe ninguna conversación entre los dos usuarios.", typeof(ProblemDetails))]
    public async Task<IActionResult> EliminarConversacion(int otroUsuarioId, CancellationToken cancellationToken)
    {
        // ArgumentException → 400 y KeyNotFoundException → 404
        // los maneja ManejoExcepcionesMiddleware automáticamente.
        var usuarioId = ObtenerUsuarioIdAutenticado();
        await _chatServicio.EliminarConversacionAsync(usuarioId, otroUsuarioId);
        return NoContent();
    }

    private int ObtenerUsuarioIdAutenticado() => User.ObtenerUsuarioIdRequerido();
}
