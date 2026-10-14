namespace ProyectoFinalTPI.Backend.Entidades
{
    /// <summary>
    /// Representa un mensaje de chat privado entre dos usuarios.
    /// Los mensajes expiran automáticamente a los 7 días de su emisión:
    /// el background service <c>LimpiezaMensajesService</c> los elimina de la BD.
    /// </summary>
    public class MensajeChat
    {
        public int Id { get; set; }

        /// <summary>FK al usuario que envió el mensaje.</summary>
        public int EmisorId { get; set; }
        public Usuario Emisor { get; set; } = null!;

        /// <summary>FK al usuario que recibió el mensaje.</summary>
        public int DestinatarioId { get; set; }
        public Usuario Destinatario { get; set; } = null!;

        /// <summary>Contenido textual del mensaje. Máximo 2000 caracteres.</summary>
        public string Contenido { get; set; } = string.Empty;

        /// <summary>
        /// Momento en que el servidor recibió y persistió el mensaje (UTC).
        /// Es la referencia para calcular la expiración de 7 días.
        /// </summary>
        public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha calculada de expiración. Igual a FechaEnvio + 7 días.
        /// Se persiste para que la consulta de limpieza use un índice simple.
        /// </summary>
        public DateTime FechaExpiracion { get; set; }
    }
}
