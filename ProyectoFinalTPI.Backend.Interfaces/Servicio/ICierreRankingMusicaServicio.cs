namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface ICierreRankingMusicaServicio
{
    Task<bool> ProcesarRonda(CancellationToken ct = default);
}
