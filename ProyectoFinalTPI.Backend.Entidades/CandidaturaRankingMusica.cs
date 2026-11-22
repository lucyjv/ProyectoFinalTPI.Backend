using System.Text;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;

namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class CandidaturaRankingMusica
{
    public const int LongitudMaximaTexto = 120;

    private CandidaturaRankingMusica() { }

    public CandidaturaRankingMusica(
        int rankingMusicaId, int creadorId, Decada decada, string titulo, string artista,
        DateTimeOffset fechaIngreso)
    {
        RankingMusicaId = rankingMusicaId;
        CreadorId = creadorId;
        Decada = decada ?? throw new ArgumentNullException(nameof(decada));
        FechaIngreso = fechaIngreso;
        CorregirDatos(titulo, artista);
    }

    public int Id { get; private set; }
    public int RankingMusicaId { get; private set; }
    public int CreadorId { get; private set; }
    public Decada Decada { get; private set; } = null!;
    public string Titulo { get; private set; } = string.Empty;
    public string Artista { get; private set; } = string.Empty;
    public string TituloNormalizado { get; private set; } = string.Empty;
    public string ArtistaNormalizado { get; private set; } = string.Empty;
    public DateTimeOffset FechaIngreso { get; private set; }
    public DateTimeOffset? FechaRetiro { get; set; }

    public void CorregirDatos(string titulo, string artista)
    {
        var tituloValidado = ValidarTexto(titulo, nameof(titulo));
        var artistaValidado = ValidarTexto(artista, nameof(artista));
        Titulo = tituloValidado;
        Artista = artistaValidado;
        TituloNormalizado = Normalizar(tituloValidado);
        ArtistaNormalizado = Normalizar(artistaValidado);
    }

    private static string ValidarTexto(string texto, string campo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texto, campo);
        var valor = texto.Trim().Normalize(NormalizationForm.FormC);
        if (valor.Length > LongitudMaximaTexto)
            throw new ArgumentException($"El campo {campo} admite hasta {LongitudMaximaTexto} caracteres.", campo);

        return valor;
    }

    private static string Normalizar(string texto) =>
        string.Join(" ", texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant().Normalize(NormalizationForm.FormC);
}
