namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class BilleteraPuntosNostalgia
{
    public int UsuarioId { get; set; }
    public int PuntosExtra { get; set; }
    public DateTimeOffset FechaActualizacion { get; set; } = DateTimeOffset.UtcNow;
}
