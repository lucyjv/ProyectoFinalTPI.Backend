namespace ProyectoFinalTPI.Backend.Entidades;

public class ReaccionComentario
{
    public int ComentarioId { get; set; }
    public Comentario Comentario { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public TipoReaccionComentario Tipo { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}

public enum TipoReaccionComentario
{
    Like,
    Love,
    Laugh,
    Surprise,
    Sad
}
