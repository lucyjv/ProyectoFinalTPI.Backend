namespace ProyectoFinalTPI.Backend.Entidades;

public class PublicacionMomento
{
    public int PublicacionId { get; set; }
    public Publicacion Publicacion { get; set; } = null!;
    public int MomentoId { get; set; }
    public Momento Momento { get; set; } = null!;
    public OrigenAsociacionMomento Origen { get; set; }
    public EstadoAsociacionMomento Estado { get; set; } = EstadoAsociacionMomento.Activo;
    public double? Confianza { get; set; }
    public string? VersionAnalisis { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}

public enum OrigenAsociacionMomento { Automatico, Autor }
public enum EstadoAsociacionMomento { Activo, Rechazado }
