namespace ProyectoFinalTPI.Backend.Servicio;

// Define los pesos, el límite de candidatos y el plazo de vistas recientes (14 días por defecto).
public sealed class FeedRecomendacionesOptions
{
    public const string Seccion = "FeedRecomendaciones";

    public int DiasDeExclusionDeImpresiones { get; set; } = 14;
    public int CantidadMaximaCandidatas { get; set; } = 500;
    public int PesoLike { get; set; } = 2;
    public int PesoPuntuacion { get; set; } = 3;
    public int PesoEtiqueta { get; set; } = 1;
    public double PesoAfinidadCategoria { get; set; } = 4d;
    public double PesoCercania { get; set; } = 3d;
    public double DistanciaDeReferenciaKm { get; set; } = 50d;
}
