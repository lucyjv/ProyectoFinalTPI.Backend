using Microsoft.AspNetCore.SignalR;
using ProyectoFinalTPI.Backend.Dtos.Notificacion;
using ProyectoFinalTPI.Backend.Hubs;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Services
{
    /// <summary>
    /// Implementación de INotificacionPush usando SignalR.
    /// Es la única clase del sistema que conoce IHubContext y NotificacionHub.
    /// Vive en la capa principal (Backend) junto a los Hubs, no en la capa Servicio.
    /// </summary>
    public class NotificacionPush : INotificacionPush
    {
        private readonly IHubContext<NotificacionHub> _hubContext;

        public NotificacionPush(IHubContext<NotificacionHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <inheritdoc/>
        public async Task EnviarAsync(
            int usuarioDestinatarioId,
            NotificacionDto notificacion,
            CancellationToken cancellationToken = default)
        {
            // El grupo del usuario es su ID como string, registrado en NotificacionHub.OnConnectedAsync.
            // Si el usuario no está conectado, SignalR descarta el mensaje silenciosamente.
            await _hubContext.Clients
                .Group(usuarioDestinatarioId.ToString())
                .SendAsync("RecibirNotificacion", notificacion, cancellationToken);
        }
    }
}
