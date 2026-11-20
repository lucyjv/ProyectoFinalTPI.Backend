namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class ConsumoDiarioPuntosNostalgia
{
    public int UsuarioId { get; init; }
    public DateOnly Fecha { get; init; }
    public int PuntosGastados { get; set; }
}
