namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class VotoRankingMusica
{
    public long Id { get; private set; }
    public int CandidaturaId { get; init; }
    public int UsuarioId { get; init; }
    public int Puntos { get; init; }
    public DateTimeOffset FechaVoto { get; init; }
}
