using ProyectoFinalTPI.Backend.Dtos.Chat;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    /// <summary>
    /// Lógica de negocio del chat: validaciones y persistencia de mensajes.
    /// La entrega en tiempo real (SignalR) es responsabilidad de ChatHub,
    /// que llama a este servicio y luego distribuye el resultado.
    /// </summary>
    public class ChatServicio : IChatServicio
    {
        private readonly IMensajeChatRepositorio _repositorio;
        private static readonly TimeSpan TiempoExpiracion = TimeSpan.FromDays(7);
        private readonly INotificacionServicio _notificacionServicio;

        public ChatServicio(
            IMensajeChatRepositorio repositorio,
            INotificacionServicio notificacionServicio)
        {
            _repositorio = repositorio;
            _notificacionServicio = notificacionServicio;
        }

        #region Operaciones públicas

        /// <inheritdoc/>
        public async Task<MensajeChatDto> EnviarMensajeAsync(
            int emisorId, string emisorUsername, int destinatarioId, string contenido)
        {
            var ahora = DateTime.UtcNow;
            var mensaje = CrearMensaje(emisorId, destinatarioId, contenido, ahora);

            await _repositorio.GuardarAsync(mensaje);

            await _notificacionServicio.NotificarMensajeChatAsync(
                destinatarioId, emisorId);
            return CrearMensajeDto(mensaje.Id, emisorId, emisorUsername, destinatarioId, contenido, ahora);
        }
        /// <inheritdoc/>
        public async Task<IEnumerable<MensajeChatDto>> ObtenerHistorialAsync(
            int usuarioAId, int usuarioBId)
        {
            var mensajes = await _repositorio.ObtenerHistorialAsync(usuarioAId, usuarioBId);

            return mensajes.Select(MapearMensaje);
        }
        /// <inheritdoc/>
        public async Task EliminarConversacionAsync(int usuarioId, int otroUsuarioId)
        {
            ValidarUsuariosDeConversacion(usuarioId, otroUsuarioId);

            var eliminados = await _repositorio.EliminarConversacionAsync(usuarioId, otroUsuarioId);

            VerificarConversacionEliminada(eliminados);
        }
        /// <inheritdoc/>
        public async Task<IEnumerable<ConversacionResumenDto>> ObtenerConversacionesActivasAsync(
            int usuarioId)
        {
            var ultimos = await _repositorio.ObtenerConversacionesActivasAsync(usuarioId);

            return ultimos.Select(mensaje => MapearConversacion(mensaje, usuarioId));
        }

        #endregion
        #region Métodos privados

        private static MensajeChatDto CrearMensajeDto(
            int id, int emisorId, string emisorUsername,
            int destinatarioId, string contenido, DateTime fechaEnvio) =>
            new()
            {
                Id = id,
                EmisorId = emisorId,
                EmisorUsername = emisorUsername,
                DestinatarioId = destinatarioId,
                Contenido = contenido,
                FechaEnvio = fechaEnvio
            };
        private static MensajeChat CrearMensaje(
            int emisorId, int destinatarioId,
            string contenido, DateTime fechaEnvio) =>
            new()
            {
                EmisorId = emisorId,
                DestinatarioId = destinatarioId,
                Contenido = contenido,
                FechaEnvio = fechaEnvio,
                FechaExpiracion = fechaEnvio.Add(TiempoExpiracion)
            };
        private static MensajeChatDto MapearMensaje(MensajeChat mensaje) =>
            CrearMensajeDto(
                mensaje.Id,
                mensaje.EmisorId,
                mensaje.Emisor?.Username ?? string.Empty,
                mensaje.DestinatarioId,
                mensaje.Contenido,
                mensaje.FechaEnvio);
        private static ConversacionResumenDto MapearConversacion(
            MensajeChat mensaje, int usuarioId)
        {
            var esEmisor = mensaje.EmisorId == usuarioId;
            var otroUsuario = esEmisor
                ? mensaje.Destinatario
                : mensaje.Emisor;
            return new ConversacionResumenDto
            {
                OtroUsuarioId = esEmisor
                    ? mensaje.DestinatarioId
                    : mensaje.EmisorId,
                OtroUsuarioUsername = otroUsuario?.Username ?? string.Empty,
                UltimoMensaje = mensaje.Contenido,
                FechaUltimoMensaje = mensaje.FechaEnvio
            };
        }
        private static void ValidarUsuariosDeConversacion(
            int usuarioId, int otroUsuarioId)
        {
            if (otroUsuarioId == usuarioId)
                throw new ArgumentException(
                    "No podés eliminar una conversación con vos mismo.");
        }
        private static void VerificarConversacionEliminada(int eliminados)
        {
            if (eliminados == 0)
                throw new KeyNotFoundException(
                    "No existe ninguna conversación entre los usuarios indicados.");
        }

        #endregion
    }
}
