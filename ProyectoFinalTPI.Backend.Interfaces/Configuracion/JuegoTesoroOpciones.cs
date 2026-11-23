namespace ProyectoFinalTPI.Backend.Interfaces.Configuracion;

public sealed class JuegoTesoroOpciones
{
    public const string Seccion = "JuegoTesoro";
    public bool Habilitado { get; set; } = true;
    public DateOnly SemanaBase { get; set; } = new(2026, 11, 22);
    public List<UbicacionTesoroOpciones> Ubicaciones { get; set; } = new();
}

public sealed class UbicacionTesoroOpciones
{
    public string Clave { get; set; } = string.Empty;
    public string Pista { get; set; } = string.Empty;
    public string NombreHallazgo { get; set; } = string.Empty;
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public double ZoomMinimo { get; set; } = 18;
}
