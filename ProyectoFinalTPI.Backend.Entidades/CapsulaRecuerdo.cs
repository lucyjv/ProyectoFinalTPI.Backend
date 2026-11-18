namespace ProyectoFinalTPI.Backend.Entidades;

// La publicación sigue siendo única; esta relación solo la incorpora a una cápsula.
public sealed class CapsulaRecuerdo
{
    public int CapsulaId { get; set; }
    public Capsula Capsula { get; set; } = null!;
    public int PublicacionId { get; set; }
    public Publicacion Publicacion { get; set; } = null!;
    public DateTime FechaIncorporacion { get; set; }
}
