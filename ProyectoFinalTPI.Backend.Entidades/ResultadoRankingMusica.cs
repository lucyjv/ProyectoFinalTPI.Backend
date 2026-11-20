using ProyectoFinalTPI.Backend.Entidades.ValueObjects;

namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class ResultadoRankingMusica
{
    public int RankingMusicaId { get; init; }
    public int CandidaturaId { get; init; }
    public required Decada Decada { get; init; }
    public int Posicion { get; init; }
    public long Puntos { get; init; }
}
