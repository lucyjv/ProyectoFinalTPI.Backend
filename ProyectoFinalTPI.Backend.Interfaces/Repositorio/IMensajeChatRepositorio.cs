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
    }
}
