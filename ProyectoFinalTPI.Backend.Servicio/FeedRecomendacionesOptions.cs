namespace ProyectoFinalTPI.Backend.Servicio;

internal static class FeedRecomendacionesOptions
{
    public const int DiasDeExclusionDeImpresiones = 14;
    public const int CantidadMaximaCandidatas = 500;
    public const double PesoAfinidadCategoria = 4d;
    public const double PesoCercania = 3d;
    public const double DistanciaDeReferenciaKm = 50d;
}
