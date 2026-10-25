using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Notificacion;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/notificaciones")]
    [Authorize]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al procesar la operación.", typeof(ProblemDetails))]
    public class NotificacionesController : ControllerBase
    {
        private readonly INotificacionServicio _notificacionServicio;

        public NotificacionesController(INotificacionServicio notificacionServicio)
        {
            _notificacionServicio = notificacionServicio;
        }

        [HttpGet]
        [SwaggerOperation(
            Summary = "Obtiene las notificaciones del usuario autenticado.",
            Description = "Requiere autenticación. Devuelve las notificaciones no expiradas, ordenadas de más reciente a más antigua."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Lista de notificaciones; puede estar vacía.", typeof(IEnumerable<NotificacionDto>))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
        public async Task<IActionResult> ObtenerNotificaciones(CancellationToken cancellationToken)
        {
            var usuarioId = ObtenerUsuarioIdAutenticado();
            var notificaciones = await _notificacionServicio.ObtenerNotificacionesAsync(
                usuarioId, cancellationToken);
            return Ok(notificaciones);
        }

        [HttpPatch("{notificacionId:int}/leida")]
        [SwaggerOperation(
            Summary = "Marca una notificación como leída.",
            Description = "Requiere autenticación. Fija la fecha de lectura e inicia el conteo de expiración de 7 días."
        )]
        [SwaggerResponse(StatusCodes.Status204NoContent, "Notificación marcada como leída.")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "La notificación no existe o no pertenece al usuario.", typeof(ProblemDetails))]
        public async Task<IActionResult> MarcarComoLeida(
            int notificacionId, CancellationToken cancellationToken)
        {
            var usuarioId = ObtenerUsuarioIdAutenticado();
            await _notificacionServicio.MarcarComoLeidaAsync(
                notificacionId, usuarioId, cancellationToken);
            return NoContent();
        }

        [HttpPatch("leidas")]
        [SwaggerOperation(
            Summary = "Marca todas las notificaciones del usuario como leídas.",
            Description = "Requiere autenticación. Fija la fecha de lectura e inicia el conteo de expiración de 7 días en cada notificación no leída."
        )]
        [SwaggerResponse(StatusCodes.Status204NoContent, "Todas las notificaciones fueron marcadas como leídas.")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
        public async Task<IActionResult> MarcarTodasComoLeidas(CancellationToken cancellationToken)
        {
            var usuarioId = ObtenerUsuarioIdAutenticado();
            await _notificacionServicio.MarcarTodasComoLeidasAsync(usuarioId, cancellationToken);
            return NoContent();
        }

        private int ObtenerUsuarioIdAutenticado() => User.ObtenerUsuarioIdRequerido();
    }
}
