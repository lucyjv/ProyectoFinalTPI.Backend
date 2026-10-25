using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Services;
using ProyectoFinalTPI.Backend.Extensions;

namespace ProyectoFinalTPI.Backend.Hubs
{
    /// <summary>
    /// Hub de SignalR que gestiona las conexiones WebSocket del chat en tiempo real.
    ///
    /// Responsabilidades de esta clase:
    ///   1. Registrar/desregistrar usuarios en el servicio de presencia.
    ///   2. Entregar mensajes en tiempo real si el destinatario está conectado.
    ///   3. Delegar la persistencia del mensaje a ChatServicio.
    ///   4. Entregar el mensaje en tiempo real al destinatario vía SignalR.
    ///
    /// El token JWT llega por query string (?access_token=...) porque los WebSockets
    /// no soportan headers HTTP personalizados. El middleware de autenticación ya
    /// está configurado en Program.cs para leerlo desde ahí.
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly PresenciaService _presencia;
        private readonly IChatServicio _chatServicio;

        public ChatHub(PresenciaService presencia, IChatServicio chatServicio)
        {
            _presencia    = presencia;
            _chatServicio = chatServicio;
        }

        /// <summary>
        /// Llamado automáticamente por SignalR cuando un cliente establece la conexión.
        /// Registra al usuario en el diccionario de presencia.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var usuarioId = ObtenerUsuarioId();
            _presencia.Conectar(usuarioId, Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Llamado automáticamente por SignalR cuando el cliente cierra la conexión.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var usuarioId = ObtenerUsuarioId();
            _presencia.Desconectar(usuarioId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Método invocable por el cliente para enviar un mensaje privado.
        /// El mensaje se persiste siempre, independientemente de si el destinatario
        /// está conectado en este momento. Si está online, lo recibe en tiempo real;
        /// si no, lo verá cuando abra el historial de la conversación.
        /// El frontend llama: connection.invoke("EnviarMensaje", destinatarioId, contenido)
        /// </summary>
        /// <param name="destinatarioId">Id del usuario destinatario.</param>
        /// <param name="contenido">Texto del mensaje. Máximo 2000 caracteres.</param>
        public async Task EnviarMensaje(int destinatarioId, string contenido)
        {
            if (string.IsNullOrWhiteSpace(contenido))
                throw new HubException("El mensaje no puede estar vacío.");

            if (contenido.Length > 2000)
                throw new HubException("El mensaje no puede superar los 2000 caracteres.");

            var emisorId = ObtenerUsuarioId();

            if (emisorId == destinatarioId)
                throw new HubException("No podés enviarte mensajes a vos mismo.");

            var emisorUsername = Context.User?.FindFirstValue("username") ?? string.Empty;

            // El servicio persiste el mensaje. No sabe nada de SignalR.
            var dto = await _chatServicio.EnviarMensajeAsync(
                emisorId, emisorUsername, destinatarioId, contenido);

            // Entrega en tiempo real: solo si el destinatario tiene una conexión activa.
            // Si está offline, el mensaje quedó guardado y lo verá al abrir el historial.
            var connectionIdDestinatario = _presencia.ObtenerConnectionId(destinatarioId);
            if (connectionIdDestinatario is not null)
                await Clients.Client(connectionIdDestinatario).SendAsync("RecibirMensaje", dto);

            // Confirmar al emisor que el mensaje fue guardado.
            await Clients.Caller.SendAsync("MensajeEnviado", dto);
        }

        /// <summary>
        /// Devuelve el historial de mensajes entre el usuario autenticado y otro usuario.
        /// El cliente llama a esto al abrir una ventana de chat para cargar mensajes previos.
        /// </summary>
        /// <param name="otroUsuarioId">Id del otro participante de la conversación.</param>
        public async Task<IEnumerable<object>> ObtenerHistorial(int otroUsuarioId)
        {
            var usuarioId = ObtenerUsuarioId();
            return await _chatServicio.ObtenerHistorialAsync(usuarioId, otroUsuarioId);
        }

        /// <summary>
        /// Extrae el Id del usuario autenticado desde los claims del JWT.
        /// El claim "sub" fue configurado en AuthServicio.GenerarJwt().
        /// </summary>
        private int ObtenerUsuarioId()
        {
            return Context.User.ObtenerUsuarioId()
                ?? throw new HubException("No se pudo identificar al usuario. Token inválido.");
        }
    }
}
