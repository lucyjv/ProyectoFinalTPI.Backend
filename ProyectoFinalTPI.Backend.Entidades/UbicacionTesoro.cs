namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class UbicacionTesoro
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Pista { get; set; } = string.Empty;
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public double ZoomMinimo { get; set; } = 18;
    public bool Activa { get; set; } = true;
}
