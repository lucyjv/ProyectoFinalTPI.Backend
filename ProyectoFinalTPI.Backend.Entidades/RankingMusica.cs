namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class RankingMusica
{
    public int Id { get; private set; }
    public DateTimeOffset Apertura { get; init; }
    public DateTimeOffset Cierre { get; init; }
    public EstadoRankingMusica Estado { get; set; } = EstadoRankingMusica.Abierto;
    public ICollection<CandidaturaRankingMusica> Candidaturas { get; } = new List<CandidaturaRankingMusica>();
}

public enum EstadoRankingMusica
{
    Abierto,
    Cerrado
}
