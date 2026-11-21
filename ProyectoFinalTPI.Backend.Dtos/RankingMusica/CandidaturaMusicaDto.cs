using System.ComponentModel.DataAnnotations;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.RankingMusica;

public class DatosCandidaturaMusicaDto
{
    [Required]
    [StringLength(CandidaturaRankingMusica.LongitudMaximaTexto)]
    public string Titulo { get; init; } = string.Empty;

    [Required]
    [StringLength(CandidaturaRankingMusica.LongitudMaximaTexto)]
    public string Artista { get; init; } = string.Empty;
}

public sealed class CrearCandidaturaMusicaDto : DatosCandidaturaMusicaDto
{
    [Range(1900, int.MaxValue)]
    public int Decada { get; init; }
}

public sealed class PuntuarMusicaDto
{
    [Range(1, int.MaxValue)]
    public int CandidaturaId { get; init; }

    [Range(1, int.MaxValue)]
    public int Puntos { get; init; }
}
