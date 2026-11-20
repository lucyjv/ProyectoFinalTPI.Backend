using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IRankingMusicaRepositorio
{
    Task<bool> CrearRondaInicial(DateTimeOffset apertura, DateTimeOffset cierre, CancellationToken ct = default);
    Task<RankingMusica?> ObtenerRondaAbierta(CancellationToken ct = default);
    Task<IReadOnlyList<CandidaturaRankingMusicaConPuntos>> ListarCandidaturas(int rankingMusicaId, CancellationToken ct = default);
    Task<RondaMusicaConResultados?> ObtenerResultados(int? rankingMusicaId = null, CancellationToken ct = default);

    Task<CandidaturaRankingMusica> AgregarCandidatura(
        int rankingMusicaId, int creadorId, Decada decada, string titulo, string artista, CancellationToken ct = default);
    Task<CandidaturaRankingMusica> CorregirCandidatura(
        int candidaturaId, int usuarioId, bool esAdministrador, string titulo, string artista, CancellationToken ct = default);
    Task RetirarCandidatura(int candidaturaId, int usuarioId, bool esAdministrador, CancellationToken ct = default);
    Task<VotoRankingMusicaRegistrado> RegistrarVoto(
        int candidaturaId, int usuarioId, int puntos, int cupoDiario, CancellationToken ct = default);
    Task<bool> CerrarRonda(int rankingMusicaId, DateTimeOffset cierreSiguiente, CancellationToken ct = default);
}

public sealed record CandidaturaRankingMusicaConPuntos(CandidaturaRankingMusica Candidatura, long Puntos);

public sealed record PosicionRankingMusica(ResultadoRankingMusica Resultado, string Titulo, string Artista);

public sealed record RondaMusicaConResultados(RankingMusica Ronda, IReadOnlyList<PosicionRankingMusica> Posiciones);

public sealed record VotoRankingMusicaRegistrado(long VotoId, long PuntosTotales, int PuntosDiariosRestantes);
