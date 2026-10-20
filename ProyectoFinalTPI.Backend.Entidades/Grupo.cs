namespace ProyectoFinalTPI.Backend.Entidades;

public class Grupo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int Decada { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public int CreadorId { get; set; }
    public Usuario Creador { get; set; } = null!;

    public ICollection<GrupoIntegrante> Integrantes { get; set; } = new List<GrupoIntegrante>();
    public ICollection<Publicacion> Recuerdos { get; set; } = new List<Publicacion>();
}
