using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Interfaces.Configuracion;

public class OpcionesAnalisisRecuerdos
{
    public const string Seccion = "AnalisisRecuerdos";
    public bool Habilitado { get; set; }
    public string Proveedor { get; set; } = "Gemini";
    public string ApiKey { get; set; } = "";
    [RegularExpression(@"^[a-z0-9][a-z0-9.-]{0,69}$")]
    public string Modelo { get; set; } = "gemini-3.5-flash-lite";
    [Range(0.5, 1)] public double ConfianzaMinima { get; set; } = 0.85;
    [Range(1, 60)] public int IntervaloMinimoSegundos { get; set; } = 6;
    [Range(1, 60)] public int IntervaloSegundos { get; set; } = 5;
    [Range(10, 900)] public int ReservaSegundos { get; set; } = 120;
    [Range(1, 300)] public int TiempoMaximoSegundos { get; set; } = 30;
    [Range(1, 10)] public int MaxIntentos { get; set; } = 3;
    [Range(1, 60)] public int ReintentoBaseSegundos { get; set; } = 10;
    [Range(100, 100000)] public int MaxCaracteresEntrada { get; set; } = 20000;
}
