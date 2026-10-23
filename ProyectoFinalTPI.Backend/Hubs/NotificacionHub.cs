using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ProyectoFinalTPI.Backend.Extensions;

namespace ProyectoFinalTPI.Backend.Hubs
{
    /// <summary>
    /// Hub de SignalR que gestiona las conexiones WebSocket de notificaciones en tiempo real.
    ///
    /// Responsabilidades de esta clase:
    ///   1. Al conectar, incorporar al usuario a su grupo personal (su ID como string),
    ///      de modo que IHubContext pueda enviarle mensajes aunque no tengamos su ConnectionId.
    ///   2. Al desconectar, sacarlo del grupo.
    ///
    /// El servicio NotificacionServicio usa IHubContext para enviar al grupo del usuario,
    /// sin necesidad de conocer el ConnectionId concreto. Esto soporta múltiples pestañas
    /// abiertas simultáneamente por el mismo usuario.
    ///
    /// El token JWT llega por query string (?access_token=...) porque los WebSockets
    /// no soportan headers HTTP personalizados. El middleware de autenticación ya
    /// está configurado en Program.cs para leerlo desde ahí (misma config que ChatHub).
    /// </summary>
    [Authorize]
    public class NotificacionHub : Hub
    {
        /// <summary>
        /// Llamado automáticamente por SignalR cuando un cliente establece la conexión.
        /// Agrega la conexión al grupo personal del usuario para que pueda recibir
        /// notificaciones aunque tenga varias pestañas abiertas.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var usuarioId = ObtenerUsuarioId();
            await Groups.AddToGroupAsync(Context.ConnectionId, usuarioId.ToString());
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Llamado automáticamente por SignalR cuando el cliente cierra la conexión.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var usuarioId = ObtenerUsuarioId();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, usuarioId.ToString());
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Extrae el Id del usuario autenticado desde los claims del JWT.
        /// Misma lógica que ChatHub.
        /// </summary>
        private int ObtenerUsuarioId()
        {
            return Context.User.ObtenerUsuarioId()
                ?? throw new HubException("No se pudo identificar al usuario. Token inválido.");
        }
    }
}
