using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio
{
    /// <summary>
    /// Contrato de acceso a datos para los mensajes de chat privado.
    /// </summary>
    public interface IMensajeChatRepositorio
    {
        /// <summary>
        /// Persiste un nuevo mensaje en la base de datos.
        /// </summary>
        Task GuardarAsync(MensajeChat mensaje);

        /// <summary>
        /// Recupera el historial de mensajes entre dos usuarios,
        /// ordenado cronológicamente (más antiguo primero).
        /// Solo devuelve mensajes aún no expirados.
        /// </summary>
        Task<IEnumerable<MensajeChat>> ObtenerHistorialAsync(int usuarioAId, int usuarioBId);

        /// <summary>
        /// Elimina todos los mensajes cuya <c>FechaExpiracion</c> ya pasó.
        /// Invocado periódicamente por <c>LimpiezaMensajesService</c>.
        /// </summary>
        Task EliminarExpiradosAsync();

        /// <summary>
        /// Devuelve todos los mensajes no expirados en los que el usuario participó
        /// (como emisor o destinatario), agrupados por el "otro usuario" del par.
        /// Por cada conversación se incluye únicamente el mensaje más reciente.
        /// Si el usuario no tiene conversaciones activas devuelve una colección vacía.
        /// </summary>
        Task<IEnumerable<MensajeChat>> ObtenerConversacionesActivasAsync(int usuarioId);
    }
}
