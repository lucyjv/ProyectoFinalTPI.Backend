namespace ProyectoFinalTPI.Backend.Dtos.RankingMusica;

public sealed record CandidaturaRankingMusicaDto(
    int Id, int CreadorId, int Decada, string Titulo, string Artista, long Puntos);

public sealed record CandidaturasPorDecadaDto(int Decada, IReadOnlyList<CandidaturaRankingMusicaDto> Candidaturas);

public sealed record RankingMusicaActualDto(
    int Id, DateTimeOffset Apertura, DateTimeOffset Cierre,
    IReadOnlyList<CandidaturasPorDecadaDto> Decadas, SaldoDiarioPuntosNostalgiaDto? Saldo);

public sealed record PosicionRankingMusicaDto(int CandidaturaId, int Posicion, string Titulo, string Artista, long Puntos);

public sealed record ResultadosPorDecadaDto(
    int Decada, IReadOnlyList<PosicionRankingMusicaDto> Ganadoras, IReadOnlyList<PosicionRankingMusicaDto> Clasificadas);

public sealed record ResultadosRankingMusicaDto(int Id, DateTimeOffset Apertura, DateTimeOffset Cierre, IReadOnlyList<ResultadosPorDecadaDto> Decadas);

public sealed record VotoRankingMusicaDto(long Id, long PuntosTotales, int PuntosDiariosRestantes);
