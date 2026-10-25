using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Servicio;

public class OpcionesGemini
{
    public bool Habilitado { get; set; }
    public string ApiKey { get; set; } = "";
    [RegularExpression(@"^[a-z0-9][a-z0-9.-]{0,69}$")]
    public string Modelo { get; set; } = "gemini-3.5-flash-lite";
    [Range(0.5, 1)] public double ConfianzaMinima { get; set; } = 0.85;
    [Range(1, 60)] public int IntervaloMinimoSegundos { get; set; } = 6;
}
