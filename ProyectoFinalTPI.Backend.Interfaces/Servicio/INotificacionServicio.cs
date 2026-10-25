using ProyectoFinalTPI.Backend.Dtos.Notificacion;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    /// <summary>
    /// Contrato de la lógica de negocio de notificaciones.
    /// Persiste la notificación y la entrega en tiempo real vía SignalR.
    /// No conoce detalles de EF Core ni de los repositorios concretos.
    /// </summary>
    public interface INotificacionServicio
    {
        /// <summary>
        /// Notifica al usuario que ganó un nuevo seguidor.
        /// </summary>
        /// <param name="usuarioDestinatarioId">Usuario que es seguido (recibe la notificación).</param>
        /// <param name="nuevoSeguidorId">Usuario que acaba de seguir al destinatario.</param>
        Task NotificarNuevoSeguidorAsync(
            int usuarioDestinatarioId, int nuevoSeguidorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifica al autor de una publicación que recibió un nuevo comentario.
        /// Solo se genera una notificación por publicación mientras no haya sido leída.
        /// </summary>
        /// <param name="usuarioDestinatarioId">Autor de la publicación.</param>
        /// <param name="publicacionId">Publicación que recibió el comentario.</param>
        /// <param name="comentadorId">Usuario que comentó.</param>
        Task NotificarComentarioEnPublicacionAsync(
            int usuarioDestinatarioId, int publicacionId, int comentadorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifica al autor de una publicación que recibió una nueva puntuación.
        /// Solo se genera una notificación por publicación mientras no haya sido leída.
        /// </summary>
        /// <param name="usuarioDestinatarioId">Autor de la publicación.</param>
        /// <param name="publicacionId">Publicación que recibió la puntuación.</param>
        /// <param name="puntuadorId">Usuario que puntuó.</param>
        Task NotificarPuntuacionEnPublicacionAsync(
            int usuarioDestinatarioId, int publicacionId, int puntuadorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifica al usuario que ascendió de categoría.
        /// PLACEHOLDER: se implementará completamente cuando el sistema de categorías esté listo.
        /// </summary>
        /// <param name="usuarioDestinatarioId">Usuario que ascendió.</param>
        Task NotificarAscensoCategoriaAsync(
            int usuarioDestinatarioId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifica al usuario que tiene mensajes sin leer en su casilla de correo.
        /// Solo se genera una notificación mientras no haya sido leída.
        /// </summary>
        /// <param name="usuarioDestinatarioId">Usuario que recibe la notificación.</param>
        Task NotificarMensajesCorreoAsync(
            int usuarioDestinatarioId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifica al usuario que recibió un mensaje de chat.
        /// Solo se genera una notificación mientras no haya sido leída.
        /// </summary>
        /// <param name="usuarioDestinatarioId">Usuario que recibe el chat.</param>
        /// <param name="emisorId">Usuario que envió el mensaje.</param>
        Task NotificarMensajeChatAsync(
            int usuarioDestinatarioId, int emisorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Devuelve todas las notificaciones no expiradas del usuario autenticado,
        /// ordenadas de más reciente a más antigua.
        /// </summary>
        Task<IEnumerable<NotificacionDto>> ObtenerNotificacionesAsync(
            int usuarioId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marca como leída una notificación específica del usuario.
        /// Lanza <see cref="KeyNotFoundException"/> si no existe o no pertenece al usuario.
        /// </summary>
        Task MarcarComoLeidaAsync(
            int notificacionId, int usuarioId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marca como leídas todas las notificaciones no leídas del usuario.
        /// </summary>
        Task MarcarTodasComoLeidasAsync(
            int usuarioId, CancellationToken cancellationToken = default);
    }
}
