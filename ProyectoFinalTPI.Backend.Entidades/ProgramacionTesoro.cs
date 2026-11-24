namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class ProgramacionTesoro
{
    public DateOnly SemanaInicio { get; set; }
    // Sin ubicación se pausa explícitamente esa semana; sin fila se usa rotación.
    public int? UbicacionId { get; set; }
}
