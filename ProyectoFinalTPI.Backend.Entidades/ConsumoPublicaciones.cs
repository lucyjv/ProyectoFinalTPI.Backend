namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class ConsumoPublicaciones
{
    public int UsuarioId { get; set; }
    public DateOnly SemanaInicio { get; set; }
    public int Cantidad { get; set; }
}