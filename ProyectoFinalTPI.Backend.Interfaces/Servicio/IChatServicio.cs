using ProyectoFinalTPI.Backend.Dtos.Chat;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    /// <summary>
    /// Contrato de la lógica de negocio del chat.
    /// Solo persiste mensajes y consulta historial.
    /// La validación de presencia y la entrega en tiempo real
    /// son responsabilidad de ChatHub.
    /// </summary>
    public interface IChatServicio
    {
        /// <summary>
        /// Persiste un mensaje de chat en la base de datos.
        /// </summary>
        /// <param name="emisorId">Id del usuario que envía.</param>
        /// <param name="emisorUsername">Username del emisor (leído del claim JWT en el Hub).</param>
        /// <param name="destinatarioId">Id del usuario que recibe.</param>
        /// <param name="contenido">Texto del mensaje.</param>
        /// <returns>El DTO del mensaje tal como fue guardado.</returns>
        Task<MensajeChatDto> EnviarMensajeAsync(
            int emisorId, string emisorUsername, int destinatarioId, string contenido);

        /// <summary>
        /// Devuelve el historial de mensajes no expirados entre dos usuarios,
        /// ordenado cronológicamente.
        /// </summary>
        Task<IEnumerable<MensajeChatDto>> ObtenerHistorialAsync(int usuarioAId, int usuarioBId);

        /// <summary>
        /// Devuelve el resumen de todas las conversaciones activas del usuario,
        /// una entrada por cada "otro usuario" con quien haya intercambiado mensajes
        /// no expirados. Cada entrada muestra el último mensaje de esa conversación.
        /// Devuelve lista vacía si no hay conversaciones activas.
        /// </summary>
        Task<IEnumerable<ConversacionResumenDto>> ObtenerConversacionesActivasAsync(int usuarioId);
    }
}
