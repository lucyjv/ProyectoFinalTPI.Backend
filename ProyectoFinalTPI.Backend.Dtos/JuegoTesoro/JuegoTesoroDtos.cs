namespace ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;

public sealed record RetoTesoroDto(int Id, string Pista, DateTime InicioUtc,
    DateTime FinUtc, double ZoomMinimo, DateTime? HalladoUtc);

public sealed record TesoroVisibleDto(int RetoId, double Latitud, double Longitud);

public sealed record HallazgoTesoroDto(int RetoId, string Nombre, DateTime HalladoUtc);

public sealed class VistaMapaTesoroDto
{
    public double Zoom { get; set; }
    public double MinLatitud { get; set; }
    public double MaxLatitud { get; set; }
    public double MinLongitud { get; set; }
    public double MaxLongitud { get; set; }
}
