namespace ProyectoFinalTPI.Backend.Dtos.Chat
{
    /// <summary>
    /// Datos de un mensaje de chat que viajan entre el servidor y los clientes.
    /// Este es el objeto que SignalR envía al navegador cuando llega un mensaje nuevo,
    /// y también el que se devuelve al pedir el historial.
    /// </summary>
    public class MensajeChatDto
    {
        public int Id { get; set; }
        public int EmisorId { get; set; }
        public string EmisorUsername { get; set; } = string.Empty;
        public int DestinatarioId { get; set; }
        public string Contenido { get; set; } = string.Empty;

        /// <summary>Timestamp UTC del momento de envío, en formato ISO 8601.</summary>
        public DateTime FechaEnvio { get; set; }
    }
}
