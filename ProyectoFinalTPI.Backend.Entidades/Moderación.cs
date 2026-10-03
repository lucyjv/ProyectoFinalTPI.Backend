namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Moderación
    {
        public int Id { get; set; }
        public string JustificacionModerador { get; set; } = string.Empty;
        public DateTime FechaCreada { get; set; }
        public DateTime FechaResuelto { get; set; }
        public MotivoEnum Motivo { get; set; }
        public EstadoEnum Estado { get; set; }
    }

    public enum EstadoEnum
    {
        Pendiente,
        Post_Ocultado,
        Usuario_Baneado,
        Rechazado
    }

    public enum MotivoEnum
    {
        Spam,
        Contenido_Ofensivo,
        Año_Incorrecto
    }
}
