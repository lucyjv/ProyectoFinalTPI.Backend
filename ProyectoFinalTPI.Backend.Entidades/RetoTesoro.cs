namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class RetoTesoro
{
    public int Id { get; set; }
    public DateOnly SemanaInicio { get; set; }
    public string ClaveUbicacion { get; set; } = string.Empty;
    public string Pista { get; set; } = string.Empty;
    public string NombreHallazgo { get; set; } = string.Empty;
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public double ZoomMinimo { get; set; }
}
