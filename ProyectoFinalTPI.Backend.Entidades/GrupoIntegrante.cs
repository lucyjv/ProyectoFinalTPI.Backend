namespace ProyectoFinalTPI.Backend.Entidades;

public class GrupoIntegrante
{
    public int GrupoId { get; set; }
    public Grupo Grupo { get; set; } = null!;

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime FechaUnion { get; set; } = DateTime.UtcNow;
}
