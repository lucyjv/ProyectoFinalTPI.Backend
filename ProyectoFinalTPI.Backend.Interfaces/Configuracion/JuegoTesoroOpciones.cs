namespace ProyectoFinalTPI.Backend.Interfaces.Configuracion;

public sealed class JuegoTesoroOpciones
{
    public const string Seccion = "JuegoTesoro";
    public bool Habilitado { get; set; } = true;
    public DateOnly SemanaBase { get; set; } = new(2026, 11, 22);
}
