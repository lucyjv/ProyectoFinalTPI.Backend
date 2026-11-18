namespace ProyectoFinalTPI.Backend.Entidades;

public enum VisibilidadCapsula { Privada, Publica }

public sealed class Capsula
{
    public int Id { get; set; }
    public int PropietarioId { get; set; }
    public Usuario Propietario { get; set; } = null!;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public VisibilidadCapsula Visibilidad { get; set; } = VisibilidadCapsula.Privada;
    public int? PortadaPublicacionId { get; set; }
    public Publicacion? PortadaPublicacion { get; set; }
    public int? PortadaAdjuntoId { get; set; }
    public AdjuntoPublicacion? PortadaAdjunto { get; set; }
    public string? PortadaRecursoId { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
    public ICollection<CapsulaRecuerdo> Recuerdos { get; set; } = new List<CapsulaRecuerdo>();
}
