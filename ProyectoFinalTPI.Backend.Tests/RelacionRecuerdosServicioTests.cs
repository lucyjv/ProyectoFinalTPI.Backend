using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class RelacionRecuerdosServicioTests
{
    private readonly RelacionRepositorioFake _repositorio = new();
    private readonly RelacionRecuerdosServicio _servicio;

    public RelacionRecuerdosServicioTests()
    {
        _servicio = new RelacionRecuerdosServicio(_repositorio);
    }

    [Fact]
    public async Task QueSeObtenganLosBloquesDeDiaYLugarDelRecuerdoOriginal()
    {
        //Arrange
        _repositorio.OriginalVisible = 10;
        var mismoDia = new RecuerdoRelacionadoDto { Id = 11, MismoDia = true };
        var mismoLugar = new RecuerdoRelacionadoDto { Id = 12, MismoDia = false };
        _repositorio.Dia.Add(mismoDia);
        _repositorio.Lugar.Add(mismoLugar);
        using var cancelacion = new CancellationTokenSource();

        //Act
        var result = await _servicio.ObtenerRelaciones(25, cancelacion.Token);

        //Assert
        Assert.Equal(25, _repositorio.IdConsultado);
        Assert.Equal(10, result.PublicacionId);
        Assert.Equal(200, result.RadioMetros);
        Assert.Same(mismoDia, Assert.Single(result.Dia.Items));
        Assert.Same(mismoLugar, Assert.Single(result.Lugar.Items));
        Assert.False(result.Dia.HayMas);
        Assert.False(result.Lugar.HayMas);
        Assert.Equal(new[] { "dia", "lugar" }, _repositorio.Consultas.Select(c => c.Tipo));
        Assert.All(_repositorio.Consultas, c =>
        {
            Assert.Equal(10, c.Id);
            Assert.Equal(7, c.Limite);
            Assert.Null(c.Cursor);
            Assert.Equal(cancelacion.Token, c.Token);
        });
    }

    [Fact]
    public async Task QueSeLimitenASeisLosRecuerdosDeCadaBloqueSinPerderElCursor()
    {
        //Arrange
        _repositorio.Dia.AddRange(Enumerable.Range(11, 7).Select(i => new RecuerdoRelacionadoDto { Id = i, DistanciaMetros = i }));
        _repositorio.Lugar.AddRange(_repositorio.Dia);

        //Act
        var result = await _servicio.ObtenerRelaciones(10, CancellationToken.None);

        //Assert
        Assert.Equal(Enumerable.Range(11, 6), result.Dia.Items.Select(p => p.Id));
        Assert.Equal(Enumerable.Range(11, 6), result.Lugar.Items.Select(p => p.Id));
        Assert.True(result.Dia.HayMas);
        Assert.True(result.Lugar.HayMas);
    }

    [Fact]
    public async Task QueSeDevuelvanBloquesVaciosSiNoHayRecuerdosRelacionados()
    {
        //Arrange
        var id = 10;

        //Act
        var result = await _servicio.ObtenerRelaciones(id, CancellationToken.None);

        //Assert
        Assert.Empty(result.Dia.Items);
        Assert.Empty(result.Lugar.Items);
        Assert.Null(result.Dia.SiguienteCursor);
        Assert.Null(result.Lugar.SiguienteCursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task QueNoSeConsultenRelacionesSiElIdNoEsPositivo(int id)
    {
        //Arrange
        var token = CancellationToken.None;

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerRelaciones(id, token));

        //Assert
        Assert.Contains("identificador", result.Message);
        Assert.Null(_repositorio.IdConsultado);
        Assert.Empty(_repositorio.Consultas);
    }

    [Fact]
    public async Task QueNoSeConsultenRelacionesSiElOrigenNoEstaDisponible()
    {
        //Arrange
        _repositorio.OriginalVisible = null;

        //Act
        var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerRelaciones(99, CancellationToken.None));

        //Assert
        Assert.Contains("no está disponible", result.Message);
        Assert.Equal(99, _repositorio.IdConsultado);
        Assert.Empty(_repositorio.Consultas);
    }

    [Theory]
    [InlineData("dia", 1)]
    [InlineData("lugar", 50)]
    public async Task QueSePuedanConsultarAmbosTiposConLosLimitesPermitidos(string tipo, int limite)
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();

        //Act
        var result = await _servicio.ObtenerRelacionadas(10, tipo, null, limite, cancelacion.Token);

        //Assert
        Assert.Empty(result.Items);
        Assert.False(result.HayMas);
        var consulta = Assert.Single(_repositorio.Consultas);
        Assert.Equal(tipo, consulta.Tipo);
        Assert.Equal(limite + 1, consulta.Limite);
        Assert.Equal(cancelacion.Token, consulta.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("momento")]
    [InlineData("DIA")]
    public async Task QueNoSeConsultenTiposDeRelacionInvalidos(string tipo)
    {
        //Arrange
        var limite = 20;

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerRelacionadas(10, tipo, null, limite, CancellationToken.None));

        //Assert
        Assert.Contains("tipo", result.Message);
        Assert.Null(_repositorio.IdConsultado);
        Assert.Empty(_repositorio.Consultas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public async Task QueNoSeConsultenLimitesFueraDelRangoPermitido(int limite)
    {
        //Arrange
        var tipo = "dia";

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerRelacionadas(10, tipo, null, limite, CancellationToken.None));

        //Assert
        Assert.Contains("límite", result.Message);
        Assert.Null(_repositorio.IdConsultado);
        Assert.Empty(_repositorio.Consultas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task QueNoSeListeUnaRelacionConUnIdInvalido(int id)
    {
        //Arrange
        var tipo = "lugar";

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerRelacionadas(id, tipo, null, 20, CancellationToken.None));

        //Assert
        Assert.Contains("identificador", result.Message);
        Assert.Null(_repositorio.IdConsultado);
        Assert.Empty(_repositorio.Consultas);
    }

    [Fact]
    public async Task QueNoSeListeUnaRelacionSiElOrigenNoEstaDisponible()
    {
        //Arrange
        _repositorio.OriginalVisible = null;

        //Act
        var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerRelacionadas(99, "lugar", null, 20, CancellationToken.None));

        //Assert
        Assert.Contains("no está disponible", result.Message);
        Assert.Empty(_repositorio.Consultas);
    }

    [Fact]
    public async Task QueSePuedaContinuarDesdeElUltimoRecuerdoEntregadoSinSaltarElAdicional()
    {
        //Arrange
        _repositorio.Dia.AddRange(new[]
        {
            new RecuerdoRelacionadoDto { Id = 11, DistanciaMetros = 50 },
            new RecuerdoRelacionadoDto { Id = 12, DistanciaMetros = 50 },
            new RecuerdoRelacionadoDto { Id = 13, DistanciaMetros = 75 }
        });

        //Act
        var primera = await _servicio.ObtenerRelacionadas(10, "dia", null, 2, CancellationToken.None);
        _repositorio.Dia.Clear();
        _repositorio.Dia.Add(new RecuerdoRelacionadoDto { Id = 13, DistanciaMetros = 75 });
        var segunda = await _servicio.ObtenerRelacionadas(10, "dia", primera.SiguienteCursor, 2, CancellationToken.None);

        //Assert
        Assert.Equal(new[] { 11, 12 }, primera.Items.Select(p => p.Id));
        Assert.True(primera.HayMas);
        var cursor = _repositorio.Consultas[1].Cursor;
        Assert.NotNull(cursor);
        Assert.Equal(12, cursor.Id);
        Assert.Equal(50, cursor.DistanciaMetros);
        Assert.Equal(10, cursor.PublicacionId);
        Assert.Equal("dia", cursor.Tipo);
        Assert.Equal(13, Assert.Single(segunda.Items).Id);
        Assert.False(segunda.HayMas);
    }

    [Fact]
    public async Task QueNoSeGenereCursorSiLaCantidadCoincideConElLimite()
    {
        //Arrange
        _repositorio.Lugar.Add(new RecuerdoRelacionadoDto { Id = 11 });
        _repositorio.Lugar.Add(new RecuerdoRelacionadoDto { Id = 12 });

        //Act
        var result = await _servicio.ObtenerRelacionadas(10, "lugar", null, 2, CancellationToken.None);

        //Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Null(result.SiguienteCursor);
        Assert.False(result.HayMas);
    }

    [Theory]
    [InlineData(25, "dia")]
    [InlineData(10, "lugar")]
    public async Task QueNoSeReutiliceUnCursorEnOtraPublicacionOTipo(int id, string tipo)
    {
        //Arrange
        _repositorio.Dia.AddRange(new[] { new RecuerdoRelacionadoDto { Id = 11 }, new RecuerdoRelacionadoDto { Id = 12 } });
        var primera = await _servicio.ObtenerRelacionadas(10, "dia", null, 1, CancellationToken.None);
        _repositorio.OriginalVisible = id;
        _repositorio.Consultas.Clear();

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerRelacionadas(id, tipo, primera.SiguienteCursor, 1, CancellationToken.None));

        //Assert
        Assert.Contains("cursor", result.Message);
        Assert.Empty(_repositorio.Consultas);
    }

    [Theory]
    [InlineData("")]
    [InlineData("basura")]
    [InlineData("e30=")]
    [InlineData("bnVsbA==")]
    [InlineData("bm8tZXMtanNvbg==")]
    public async Task QueNoSeConsulteConUnCursorMalformado(string cursor)
    {
        //Arrange
        var tipo = "dia";

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerRelacionadas(10, tipo, cursor, 20, CancellationToken.None));

        //Assert
        Assert.Contains("cursor", result.Message);
        Assert.Empty(_repositorio.Consultas);
    }

    [Fact]
    public async Task QueNoSeConsulteConUnCursorDemasiadoLargo()
    {
        //Arrange
        var cursor = new string('a', 513);

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerRelacionadas(10, "dia", cursor, 20, CancellationToken.None));

        //Assert
        Assert.Contains("cursor", result.Message);
        Assert.Empty(_repositorio.Consultas);
    }

    [Theory]
    [InlineData(-0.001, 11)]
    [InlineData(200.001, 11)]
    [InlineData(50, 0)]
    public async Task QueNoSeConsulteConUnCursorConDistanciaOIdInvalidos(double distancia, int id)
    {
        //Arrange
        var cursor = Convert.ToBase64String(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            new CursorRelacionDto(10, "dia", distancia, id)));

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerRelacionadas(10, "dia", cursor, 20, CancellationToken.None));

        //Assert
        Assert.Contains("cursor", result.Message);
        Assert.Empty(_repositorio.Consultas);
    }

    [Fact]
    public async Task QueSePropagueLaCancelacionDeLaConsulta()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();

        //Act
        var result = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _servicio.ObtenerRelaciones(10, cancelacion.Token));

        //Assert
        Assert.Equal(cancelacion.Token, result.CancellationToken);
        Assert.Empty(_repositorio.Consultas);
    }

    private sealed record Consulta(int Id, string Tipo, CursorRelacionDto? Cursor, int Limite, CancellationToken Token);

    private sealed class RelacionRepositorioFake : IRelacionRecuerdosRepositorio
    {
        public int? OriginalVisible = 10;
        public int? IdConsultado;
        public List<RecuerdoRelacionadoDto> Dia = new();
        public List<RecuerdoRelacionadoDto> Lugar = new();
        public List<Consulta> Consultas = new();

        public Task<int?> ObtenerOriginalVisible(int id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            IdConsultado = id;
            return Task.FromResult(OriginalVisible);
        }

        public Task<List<RecuerdoRelacionadoDto>> Obtener(int id, string tipo, CursorRelacionDto? cursor, int limite, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Consultas.Add(new Consulta(id, tipo, cursor, limite, ct));
            return Task.FromResult((tipo == "dia" ? Dia : Lugar).ToList());
        }
    }
}
