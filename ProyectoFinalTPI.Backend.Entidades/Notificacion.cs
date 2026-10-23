namespace ProyectoFinalTPI.Backend.Entidades
{
    /// <summary>
    /// Representa una notificación generada por el sistema para un usuario destinatario.
    /// Las notificaciones expiran automáticamente a los 7 días de haber sido leídas:
    /// el background service <c>LimpiezaNotificacionesService</c> las elimina de la BD.
    /// Mientras no sean leídas, no expiran.
    /// </summary>
    public class Notificacion
    {
        public int Id { get; set; }

        /// <summary>FK al usuario que debe recibir la notificación.</summary>
        public int UsuarioDestinatarioId { get; set; }
        public Usuario UsuarioDestinatario { get; set; } = null!;

        /// <summary>Tipo de evento que originó la notificación.</summary>
        public TipoNotificacion Tipo { get; set; }

        /// <summary>Indica si el usuario ya vio la notificación.</summary>
        public bool Leida { get; set; } = false;

        /// <summary>Momento en que el sistema generó la notificación (UTC).</summary>
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Momento en que el usuario marcó la notificación como leída (UTC).
        /// Null si todavía no fue leída.
        /// </summary>
        public DateTime? FechaLeida { get; set; }

        /// <summary>
        /// Fecha calculada de expiración. Igual a FechaLeida + 7 días.
        /// Null mientras la notificación no haya sido leída (no expira hasta leerse).
        /// Se persiste para que la consulta de limpieza use un índice simple.
        /// </summary>
        public DateTime? FechaExpiracion { get; set; }

        // ── Contexto de navegación (todos opcionales según el tipo) ──────────

        /// <summary>
        /// Id de la publicación relacionada.
        /// Presente en: ComentarioEnPublicacion, PuntuacionEnPublicacion.
        /// </summary>
        public int? PublicacionId { get; set; }

        /// <summary>
        /// Id del usuario que disparó el evento (el nuevo seguidor, quien comentó, etc.).
        /// Presente en: NuevoSeguidor, ComentarioEnPublicacion, PuntuacionEnPublicacion, MensajeChat.
        /// </summary>
        public int? EmisorId { get; set; }
        public Usuario? Emisor { get; set; }
    }

    public enum TipoNotificacion
    {
        NuevoSeguidor,
        ComentarioEnPublicacion,
        PuntuacionEnPublicacion,
        AscensoCategoria,
        MensajesCorreo,
        MensajeChat
    }
}
