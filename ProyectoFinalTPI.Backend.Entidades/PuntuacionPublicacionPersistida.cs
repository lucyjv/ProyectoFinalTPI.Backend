namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class PuntuacionPublicacionPersistida
{
    public long Id { get; private set; }
    public int UsuarioId { get; init; }
    public int PublicacionId { get; init; }
    public int? Puntaje { get; init; }
    public DateTimeOffset FechaActualizacion { get; init; }
    public bool PendienteDeProyectar { get; init; }
}
