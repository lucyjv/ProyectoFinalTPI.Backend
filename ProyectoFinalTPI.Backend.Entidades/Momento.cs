namespace ProyectoFinalTPI.Backend.Entidades;

public class Momento
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? ClaveIdentidad { get; set; }
    public long VersionCatalogo { get; set; } = 1;
    public int? MomentoCanonicoId { get; set; }
    public Momento? MomentoCanonico { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int? MomentoPadreId { get; set; }
    public Momento? MomentoPadre { get; set; }
    public ICollection<Momento> Hijos { get; set; } = new List<Momento>();
    public ICollection<MomentoAlias> Alias { get; set; } = new List<MomentoAlias>();
    public ICollection<PublicacionMomento> Publicaciones { get; set; } = new List<PublicacionMomento>();
}
