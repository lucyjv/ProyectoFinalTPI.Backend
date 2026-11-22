using ProyectoFinalTPI.Backend.Dtos.RankingMusica;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IRankingMusicaServicio
{
    Task<RankingMusicaActualDto?> ObtenerActual(int? usuarioId = null, CancellationToken ct = default);
    Task<ResultadosRankingMusicaDto?> ObtenerResultados(int? rankingMusicaId = null, CancellationToken ct = default);
    Task<CandidaturaRankingMusicaDto> SugerirMusica(int usuarioId, int decada, string titulo, string artista, CancellationToken ct = default);
    Task<CandidaturaRankingMusicaDto> AgregarMusicaComoAdministrador(
        int usuarioId, bool esAdministrador, int decada, string titulo, string artista, CancellationToken ct = default);
    Task<CandidaturaRankingMusicaDto> EditarCandidatura(
        int candidaturaId, int usuarioId, bool esAdministrador, string titulo, string artista, CancellationToken ct = default);
    Task RetirarCandidatura(int candidaturaId, int usuarioId, bool esAdministrador, CancellationToken ct = default);
    Task<VotoRankingMusicaDto> PuntuarMusica(int candidaturaId, int usuarioId, int puntos, CancellationToken ct = default);

    /// <summary>Procesa la ronda si corresponde y devuelve la fecha del próximo cierre.</summary>
    Task<DateTimeOffset?> ProcesarRonda(CancellationToken ct = default);
}
