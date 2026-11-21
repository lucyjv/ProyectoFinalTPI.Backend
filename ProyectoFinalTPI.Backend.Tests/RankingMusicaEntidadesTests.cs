using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class RankingMusicaEntidadesTests
{
    private static readonly DateTimeOffset FechaIngreso = new(2026, 4, 1, 3, 0, 0, TimeSpan.Zero);
    private static readonly Decada AñosOchenta = new(1980);

    [Fact]
    public void QueUnaRondaNuevaEmpieceAbiertaYSinCandidaturas()
    {
        // Arrange
        var apertura = FechaIngreso;
        var cierre = apertura.AddDays(15);

        // Act
        var ronda = new RankingMusica { Apertura = apertura, Cierre = cierre };

        // Assert
        Assert.Equal(EstadoRankingMusica.Abierto, ronda.Estado);
        Assert.Empty(ronda.Candidaturas);
    }

    [Fact]
    public void QueLaCandidaturaConserveElTextoVisibleYNormaliceLasClavesDeDuplicados()
    {
        // Arrange
        const string titulo = "  Cancio\u0301n  del ayer  ";
        const string artista = "  La\tBanda Original ";

        // Act
        var candidatura = new CandidaturaRankingMusica(7, 21, AñosOchenta, titulo, artista, FechaIngreso);

        // Assert
        Assert.Equal("Canción  del ayer", candidatura.Titulo);
        Assert.Equal("La\tBanda Original", candidatura.Artista);
        Assert.Equal("CANCIÓN DEL AYER", candidatura.TituloNormalizado);
        Assert.Equal("LA BANDA ORIGINAL", candidatura.ArtistaNormalizado);
        Assert.Null(candidatura.FechaRetiro);
    }

    [Theory]
    [InlineData(null, "Banda", "titulo")]
    [InlineData("", "Banda", "titulo")]
    [InlineData(" \t ", "Banda", "titulo")]
    [InlineData("Canción", null, "artista")]
    [InlineData("Canción", " \t ", "artista")]
    public void QueUnaCandidaturaRequieraTituloYArtista(string? titulo, string? artista, string campo)
    {
        // Arrange
        var decada = AñosOchenta;

        // Act
        var excepcion = Record.Exception(() =>
            new CandidaturaRankingMusica(7, 21, decada, titulo!, artista!, FechaIngreso));

        // Assert
        var error = Assert.IsAssignableFrom<ArgumentException>(excepcion);
        Assert.Equal(campo, error.ParamName);
    }

    [Fact]
    public void QueUnaCorreccionInvalidaNoCambieNingunCampoDeLaCandidatura()
    {
        // Arrange
        var candidatura = new CandidaturaRankingMusica(7, 21, AñosOchenta, "Canción", "Banda", FechaIngreso);
        var artistaLargo = new string('a', CandidaturaRankingMusica.LongitudMaximaTexto + 1);

        // Act
        var error = Assert.Throws<ArgumentException>(() => candidatura.CorregirDatos("Otro título", artistaLargo));

        // Assert
        Assert.Equal("artista", error.ParamName);
        Assert.Equal("Canción", candidatura.Titulo);
        Assert.Equal("CANCIÓN", candidatura.TituloNormalizado);
        Assert.Equal("Banda", candidatura.Artista);
        Assert.Equal("BANDA", candidatura.ArtistaNormalizado);
    }

    [Fact]
    public void QueCorregirLaCandidaturaActualiceSusClavesSinCambiarLaRondaNiLaFechaDeIngreso()
    {
        // Arrange
        var candidatura = new CandidaturaRankingMusica(7, 21, AñosOchenta, "Canción", "Banda", FechaIngreso);
        var titulo = new string('a', CandidaturaRankingMusica.LongitudMaximaTexto);

        // Act
        candidatura.CorregirDatos(titulo, "  Otra  banda ");

        // Assert
        Assert.Equal(titulo, candidatura.Titulo);
        Assert.Equal(titulo.ToUpperInvariant(), candidatura.TituloNormalizado);
        Assert.Equal("Otra  banda", candidatura.Artista);
        Assert.Equal("OTRA BANDA", candidatura.ArtistaNormalizado);
        Assert.Equal(7, candidatura.RankingMusicaId);
        Assert.Equal(21, candidatura.CreadorId);
        Assert.Equal(AñosOchenta, candidatura.Decada);
        Assert.Equal(FechaIngreso, candidatura.FechaIngreso);
    }
}
