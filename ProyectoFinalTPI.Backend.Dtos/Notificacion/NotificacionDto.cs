using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Notificacion
{
    /// <summary>
    /// DTO que el backend entrega al frontend para representar una notificación.
    /// El frontend usa <see cref="Tipo"/> y los campos de contexto opcionales
    /// para construir el texto a mostrar y la URL de redirección.
    /// </summary>
    public class NotificacionDto
    {
        public int Id { get; set; }

        /// <summary>
        /// Tipo de evento. El frontend lo usa para determinar
        /// el ícono, el texto y la ruta de navegación.
        /// </summary>
        public TipoNotificacion Tipo { get; set; }

        public bool Leida { get; set; }

        public DateTime FechaCreacion { get; set; }

        // ── Contexto de navegación (opcionales según tipo) ───────────────────

        /// <summary>
        /// Id de la publicación involucrada.
        /// Presente en: ComentarioEnPublicacion, PuntuacionEnPublicacion.
        /// El frontend lo usa para armar la ruta /publicaciones/{PublicacionId}.
        /// </summary>
        public int? PublicacionId { get; set; }

        /// <summary>
        /// Id del usuario que originó el evento.
        /// Presente en: NuevoSeguidor, ComentarioEnPublicacion, PuntuacionEnPublicacion, MensajeChat.
        /// </summary>
        public int? EmisorId { get; set; }

        /// <summary>Username del usuario que originó el evento.</summary>
        public string? EmisorUsername { get; set; }
    }
}
