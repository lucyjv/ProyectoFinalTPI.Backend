using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.RankingMusica;
using ProyectoFinalTPI.Backend.Entidades;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class RankingMusicaDtoTests
{
    private static readonly DateTimeOffset Apertura = new(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData("", "Artista", nameof(DatosCandidaturaMusicaDto.Titulo))]
    [InlineData("Canción", "  ", nameof(DatosCandidaturaMusicaDto.Artista))]
    public void QueElTituloYElArtistaSeanObligatorios(string titulo, string artista, string campo)
    {
        var dto = new DatosCandidaturaMusicaDto { Titulo = titulo, Artista = artista };

        Assert.Equal(campo, Assert.Single(CamposInvalidos(dto)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void QueLosTextosDeLaCandidaturaRespetenLaLongitudMaxima(int exceso, bool esValida)
    {
        var texto = new string('a', CandidaturaRankingMusica.LongitudMaximaTexto + exceso);
        var dto = new CrearCandidaturaMusicaDto { Decada = 1990, Titulo = texto, Artista = texto };

        var campos = CamposInvalidos(dto);

        if (esValida)
            Assert.Empty(campos);
        else
            Assert.Equal(new[] { nameof(dto.Artista), nameof(dto.Titulo) }, campos.Order());
    }

    [Theory]
    [InlineData(1899, false)]
    [InlineData(1900, true)]
    public void QueLaDecadaDeUnaCandidaturaTengaUnMinimo(int decada, bool esValida)
    {
        var dto = new CrearCandidaturaMusicaDto { Decada = decada, Titulo = "Canción", Artista = "Artista" };

        var campos = CamposInvalidos(dto);

        if (esValida)
            Assert.Empty(campos);
        else
            Assert.Equal(nameof(dto.Decada), Assert.Single(campos));
    }

    [Theory]
    [InlineData(0, 1, nameof(PuntuarMusicaDto.CandidaturaId))]
    [InlineData(1, 0, nameof(PuntuarMusicaDto.Puntos))]
    [InlineData(1, -1, nameof(PuntuarMusicaDto.Puntos))]
    [InlineData(1, 1, null)]
    public void QueUnVotoRequieraIdentificadorYPuntosPositivos(int candidaturaId, int puntos, string? campo)
    {
        var dto = new PuntuarMusicaDto { CandidaturaId = candidaturaId, Puntos = puntos };

        var campos = CamposInvalidos(dto);

        if (campo is null)
            Assert.Empty(campos);
        else
            Assert.Equal(campo, Assert.Single(campos));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueLaRondaActualAgrupeCandidaturasYExpongaElSaldoCuandoEstaDisponible(bool incluirSaldo)
    {
        var candidatura = new CandidaturaRankingMusicaDto(42, 7, 1990, "Canción", "Artista", 3_000_000_000L);
        var saldo = incluirSaldo ? new SaldoDiarioPuntosNostalgiaDto(5, 3, Apertura.AddDays(1), 15) : null;
        var ronda = new RankingMusicaActualDto(1, Apertura, Apertura.AddDays(7),
            [new CandidaturasPorDecadaDto(1990, [candidatura])], saldo);

        var json = JsonSerializer.SerializeToElement(ronda, JsonSerializerOptions.Web);

        var decada = Assert.Single(json.GetProperty("decadas").EnumerateArray());
        Assert.Equal(1990, decada.GetProperty("decada").GetInt32());
        var cancion = Assert.Single(decada.GetProperty("candidaturas").EnumerateArray());
        Assert.Equal(42, cancion.GetProperty("id").GetInt32());
        Assert.Equal(7, cancion.GetProperty("creadorId").GetInt32());
        Assert.Equal(3_000_000_000L, cancion.GetProperty("puntos").GetInt64());
        var saldoJson = json.GetProperty("saldo");
        if (incluirSaldo)
            Assert.Equal(18, saldoJson.GetProperty("totalDisponible").GetInt32());
        else
            Assert.Equal(JsonValueKind.Null, saldoJson.ValueKind);
    }

    [Fact]
    public void QueLosResultadosExponganGanadorasYClasificadasEnSusColecciones()
    {
        var ganadora = new PosicionRankingMusicaDto(42, 1, "Ganadora", "Artista A", 3_000_000_000L);
        var clasificada = new PosicionRankingMusicaDto(43, 2, "Clasificada", "Artista B", 20L);
        var resultados = new ResultadosRankingMusicaDto(1, Apertura, Apertura.AddDays(7),
            [new ResultadosPorDecadaDto(1990, [ganadora], [clasificada])]);

        var json = JsonSerializer.SerializeToElement(resultados, JsonSerializerOptions.Web);

        Assert.Equal(1, json.GetProperty("id").GetInt32());
        var decada = Assert.Single(json.GetProperty("decadas").EnumerateArray());
        Assert.Equal(1990, decada.GetProperty("decada").GetInt32());
        var primera = Assert.Single(decada.GetProperty("ganadoras").EnumerateArray());
        var segunda = Assert.Single(decada.GetProperty("clasificadas").EnumerateArray());
        Assert.Equal(42, primera.GetProperty("candidaturaId").GetInt32());
        Assert.Equal(1, primera.GetProperty("posicion").GetInt32());
        Assert.Equal(3_000_000_000L, primera.GetProperty("puntos").GetInt64());
        Assert.Equal(43, segunda.GetProperty("candidaturaId").GetInt32());
        Assert.Equal(2, segunda.GetProperty("posicion").GetInt32());
    }

    [Fact]
    public void QueElVotoExpongaTotalesDe64BitsYElCupoRestante()
    {
        var voto = new VotoRankingMusicaDto(4_000_000_000L, 3_000_000_000L, 2);

        var json = JsonSerializer.SerializeToElement(voto, JsonSerializerOptions.Web);

        Assert.Equal(4_000_000_000L, json.GetProperty("id").GetInt64());
        Assert.Equal(3_000_000_000L, json.GetProperty("puntosTotales").GetInt64());
        Assert.Equal(2, json.GetProperty("puntosDiariosRestantes").GetInt32());
    }

    private static string[] CamposInvalidos(object dto)
    {
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), errores, validateAllProperties: true);
        return errores.SelectMany(error => error.MemberNames).ToArray();
    }
}
