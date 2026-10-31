using Moq;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class RelacionRecuerdosServicioTests
{
    private readonly Mock<IRelacionRecuerdosRepositorio> _repositorioMock = new();
    private readonly RelacionRecuerdosServicio _servicio;

    public RelacionRecuerdosServicioTests()
    {
        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return 10;
            });

        _repositorioMock
            .Setup(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecuerdoRelacionadoDto>());

        _servicio = new RelacionRecuerdosServicio(_repositorioMock.Object);
    }

    #region Obtener Relaciones Generales

    [Fact]
    public async Task QueSeObtenganLosBloquesDeDiaYLugarDelRecuerdoOriginal()
    {
        //Arrange
        var mismoDia = new RecuerdoRelacionadoDto { Id = 11, MismoDia = true };
        var mismoLugar = new RecuerdoRelacionadoDto { Id = 12, MismoDia = false };
        using var cancelacion = new CancellationTokenSource();

        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(25, cancelacion.Token))
            .ReturnsAsync(10);
        _repositorioMock
            .Setup(r => r.Obtener(10, "dia", null, 7, cancelacion.Token))
            .ReturnsAsync(new List<RecuerdoRelacionadoDto> { mismoDia });
        _repositorioMock
            .Setup(r => r.Obtener(10, "lugar", null, 7, cancelacion.Token))
            .ReturnsAsync(new List<RecuerdoRelacionadoDto> { mismoLugar });

        //Act
        var result = await _servicio.ObtenerRelaciones(25, cancelacion.Token);

        //Assert
        Assert.Equal(10, result.PublicacionId);
        Assert.Equal(200, result.RadioMetros);
        Assert.Same(mismoDia, Assert.Single(result.Dia.Items));
        Assert.Same(mismoLugar, Assert.Single(result.Lugar.Items));
        Assert.False(result.Dia.HayMas);
        Assert.False(result.Lugar.HayMas);
        _repositorioMock.Verify(r => r.ObtenerOriginalVisible(25, cancelacion.Token), Times.Once);
        _repositorioMock.Verify(r => r.Obtener(10, "dia", null, 7, cancelacion.Token), Times.Once);
        _repositorioMock.Verify(r => r.Obtener(10, "lugar", null, 7, cancelacion.Token), Times.Once);
    }

    [Fact]
    public async Task QueSeLimitenASeisLosRecuerdosDeCadaBloqueSinPerderElCursor()
    {
        //Arrange
        var items = Enumerable.Range(11, 7).Select(i => new RecuerdoRelacionadoDto { Id = i, DistanciaMetros = i }).ToList();
        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _repositorioMock
            .Setup(r => r.Obtener(10, "dia", null, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.ToList());
        _repositorioMock
            .Setup(r => r.Obtener(10, "lugar", null, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.ToList());

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
        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _repositorioMock
            .Setup(r => r.Obtener(id, It.IsAny<string>(), null, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecuerdoRelacionadoDto>());

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
        _repositorioMock.Verify(r => r.ObtenerOriginalVisible(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositorioMock.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNoSeConsultenRelacionesSiElOrigenNoEstaDisponible()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        //Act
        var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerRelaciones(99, CancellationToken.None));

        //Assert
        Assert.Contains("no está disponible", result.Message);
        _repositorioMock.Verify(r => r.ObtenerOriginalVisible(99, It.IsAny<CancellationToken>()), Times.Once);
        _repositorioMock.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Obtener Relacionadas Paginadas

    [Theory]
    [InlineData("dia", 1)]
    [InlineData("lugar", 50)]
    public async Task QueSePuedanConsultarAmbosTiposConLosLimitesPermitidos(string tipo, int limite)
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();
        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(10, cancelacion.Token))
            .ReturnsAsync(10);
        _repositorioMock
            .Setup(r => r.Obtener(10, tipo, null, limite + 1, cancelacion.Token))
            .ReturnsAsync(new List<RecuerdoRelacionadoDto>());

        //Act
        var result = await _servicio.ObtenerRelacionadas(10, tipo, null, limite, cancelacion.Token);

        //Assert
        Assert.Empty(result.Items);
        Assert.False(result.HayMas);
        _repositorioMock.Verify(r => r.Obtener(10, tipo, null, limite + 1, cancelacion.Token), Times.Once);
    }

    [Fact]
    public async Task QueSePuedaContinuarDesdeElUltimoRecuerdoEntregadoSinSaltarElAdicional()
    {
        //Arrange
        var itemsPrimera = new List<RecuerdoRelacionadoDto>
        {
            new() { Id = 11, DistanciaMetros = 50 },
            new() { Id = 12, DistanciaMetros = 50 },
            new() { Id = 13, DistanciaMetros = 75 }
        };
        var itemsSegunda = new List<RecuerdoRelacionadoDto>
        {
            new() { Id = 13, DistanciaMetros = 75 }
        };

        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _repositorioMock
            .Setup(r => r.Obtener(10, "dia", null, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(itemsPrimera);
        _repositorioMock
            .Setup(r => r.Obtener(10, "dia", It.Is<CursorRelacionDto?>(c => c != null && c.Id == 12 && c.DistanciaMetros == 50), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(itemsSegunda);

        //Act
        var primera = await _servicio.ObtenerRelacionadas(10, "dia", null, 2, CancellationToken.None);
        var segunda = await _servicio.ObtenerRelacionadas(10, "dia", primera.SiguienteCursor, 2, CancellationToken.None);

        //Assert
        Assert.Equal(new[] { 11, 12 }, primera.Items.Select(p => p.Id));
        Assert.True(primera.HayMas);
        Assert.Equal(13, Assert.Single(segunda.Items).Id);
        Assert.False(segunda.HayMas);
        _repositorioMock.Verify(r => r.Obtener(
            10,
            "dia",
            It.Is<CursorRelacionDto?>(c => c != null && c.Id == 12 && c.DistanciaMetros == 50 && c.PublicacionId == 10 && c.Tipo == "dia"),
            3,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNoSeGenereCursorSiLaCantidadCoincideConElLimite()
    {
        //Arrange
        var items = new List<RecuerdoRelacionadoDto>
        {
            new() { Id = 11 },
            new() { Id = 12 }
        };
        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _repositorioMock
            .Setup(r => r.Obtener(10, "lugar", null, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        //Act
        var result = await _servicio.ObtenerRelacionadas(10, "lugar", null, 2, CancellationToken.None);

        //Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Null(result.SiguienteCursor);
        Assert.False(result.HayMas);
    }

    #endregion

    #region Validaciones de Cursor y Parámetros

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
        _repositorioMock.Verify(r => r.ObtenerOriginalVisible(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositorioMock.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _repositorioMock.Verify(r => r.ObtenerOriginalVisible(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositorioMock.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _repositorioMock.Verify(r => r.ObtenerOriginalVisible(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositorioMock.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNoSeListeUnaRelacionSiElOrigenNoEstaDisponible()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        //Act
        var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerRelacionadas(99, "lugar", null, 20, CancellationToken.None));

        //Assert
        Assert.Contains("no está disponible", result.Message);
        _repositorioMock.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(25, "dia")]
    [InlineData(10, "lugar")]
    public async Task QueNoSeReutiliceUnCursorEnOtraPublicacionOTipo(int id, string tipo)
    {
        //Arrange
        var items = new List<RecuerdoRelacionadoDto>
        {
            new() { Id = 11 },
            new() { Id = 12 }
        };
        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _repositorioMock
            .Setup(r => r.Obtener(10, "dia", null, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var primera = await _servicio.ObtenerRelacionadas(10, "dia", null, 1, CancellationToken.None);
        _repositorioMock
            .Setup(r => r.ObtenerOriginalVisible(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(id);

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() =>
            _servicio.ObtenerRelacionadas(id, tipo, primera.SiguienteCursor, 1, CancellationToken.None));

        //Assert
        Assert.Contains("cursor", result.Message);
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
        var result = await Assert.ThrowsAsync<ArgumentException>(() =>
            _servicio.ObtenerRelacionadas(10, tipo, cursor, 20, CancellationToken.None));

        //Assert
        Assert.Contains("cursor", result.Message);
        _repositorioMock.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNoSeConsulteConUnCursorDemasiadoLargo()
    {
        //Arrange
        var cursor = new string('a', 513);

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() =>
            _servicio.ObtenerRelacionadas(10, "dia", cursor, 20, CancellationToken.None));

        //Assert
        Assert.Contains("cursor", result.Message);
        _repositorioMock.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var result = await Assert.ThrowsAsync<ArgumentException>(() =>
            _servicio.ObtenerRelacionadas(10, "dia", cursor, 20, CancellationToken.None));

        //Assert
        Assert.Contains("cursor", result.Message);
        _repositorioMock.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Cancelación

    [Fact]
    public async Task QueSePropagueLaCancelacionDeLaConsulta()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();

        //Act
        var result = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _servicio.ObtenerRelaciones(10, cancelacion.Token));

        //Assert
        Assert.Equal(cancelacion.Token, result.CancellationToken);
        _repositorioMock.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CursorRelacionDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
