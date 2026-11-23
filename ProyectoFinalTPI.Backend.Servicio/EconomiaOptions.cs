namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class EconomiaOptions
{
    public const string Seccion = "Economia";
    public decimal CotizacionDolarFallback { get; set; } = 1200m;
    public string DolarApiUrl { get; set; } = "https://dolarapi.com/v1/dolares/oficial";
    public int CacheMinutos { get; set; } = 240;
}
