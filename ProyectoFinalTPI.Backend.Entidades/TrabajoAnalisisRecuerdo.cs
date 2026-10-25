namespace ProyectoFinalTPI.Backend.Entidades;

public class TrabajoAnalisisRecuerdo
{
    public long Id { get; set; }
    public int PublicacionId { get; set; }
    public Publicacion Publicacion { get; set; } = null!;
    public long VersionContenido { get; set; }
    public EstadoTrabajoAnalisis Estado { get; set; }
    public int Intentos { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
    public DateTime ProximoIntento { get; set; } = DateTime.UtcNow;
    public Guid? TokenReserva { get; set; }
    public DateTime? ReservadoHasta { get; set; }
    public string? CodigoError { get; set; }
    public string? VersionAnalizador { get; set; }
}

public enum EstadoTrabajoAnalisis { Pendiente, Procesando, Completado, SinMomento, Descartado, Fallido, SinAnalizador }
