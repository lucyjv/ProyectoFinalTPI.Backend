namespace ProyectoFinalTPI.Backend.Dtos.Chat
{
    /// <summary>
    /// Resumen de una conversación activa del usuario autenticado.
    /// Representa el "otro lado" de la conversación y el último mensaje intercambiado.
    /// </summary>
    public class ConversacionResumenDto
    {
        public int OtroUsuarioId { get; set; }
        public string OtroUsuarioUsername { get; set; } = string.Empty;
        public string UltimoMensaje { get; set; } = string.Empty;
        public DateTime FechaUltimoMensaje { get; set; }
    }
}
