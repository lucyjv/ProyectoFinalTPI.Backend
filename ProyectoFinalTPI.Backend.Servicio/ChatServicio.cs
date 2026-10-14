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

        public ChatServicio(IMensajeChatRepositorio repositorio)
        {
            _repositorio = repositorio;
        }

        /// <inheritdoc/>
        public async Task<MensajeChatDto> EnviarMensajeAsync(
            int emisorId, string emisorUsername, int destinatarioId, string contenido)
        {
            var ahora = DateTime.UtcNow;
            var mensaje = new MensajeChat
            {
                EmisorId       = emisorId,
                DestinatarioId = destinatarioId,
                Contenido      = contenido,
                FechaEnvio     = ahora,
                FechaExpiracion = ahora.Add(TiempoExpiracion)
            };

            await _repositorio.GuardarAsync(mensaje);

            return new MensajeChatDto
            {
                Id             = mensaje.Id,
                EmisorId       = emisorId,
                EmisorUsername = emisorUsername,
                DestinatarioId = destinatarioId,
                Contenido      = contenido,
                FechaEnvio     = ahora
            };
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<MensajeChatDto>> ObtenerHistorialAsync(
            int usuarioAId, int usuarioBId)
        {
            var mensajes = await _repositorio.ObtenerHistorialAsync(usuarioAId, usuarioBId);

            return mensajes.Select(m => new MensajeChatDto
            {
                Id             = m.Id,
                EmisorId       = m.EmisorId,
                EmisorUsername = m.Emisor?.Username ?? string.Empty,
                DestinatarioId = m.DestinatarioId,
                Contenido      = m.Contenido,
                FechaEnvio     = m.FechaEnvio
            });
        }
    }
}
