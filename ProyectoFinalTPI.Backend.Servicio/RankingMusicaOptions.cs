namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class RankingMusicaOptions
{
    public const string Seccion = "RankingMusica";

    public DateOnly? FechaInicio { get; set; }
    public int DuracionDias { get; set; } = 15;
}
