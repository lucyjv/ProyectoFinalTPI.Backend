using Moq;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ExploracionMomentosServicioTests
{
    private readonly Mock<IExploracionMomentosRepositorio> _repositorioMock = new();
    private readonly Mock<IRelacionRecuerdosRepositorio> _relacionesMock = new();
    private readonly ExploracionMomentosServicio _servicio;
    private int? _original = 10;
    private MomentoDetalleDto? _detalle = new() { Id = 1 };
    private MomentosPublicacionDto _estado = new() { PublicacionId = 10 };

    public ExploracionMomentosServicioTests()
    {
        _relacionesMock.Setup(r => r.ObtenerOriginalVisible(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int id, CancellationToken ct) => Task.FromResult(_original));

        _repositorioMock.Setup(r => r.ObtenerDetalle(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int id, CancellationToken ct) => Task.FromResult<MomentoDetalleDto?>(_detalle));

        _repositorioMock.Setup(r => r.ObtenerEstado(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns((int id, int? uid, CancellationToken ct) => Task.FromResult<MomentosPublicacionDto?>(_estado));

        _repositorioMock.Setup(r => r.Corregir(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((int id, int uid, IReadOnlyCollection<int> mids, bool perm, long v, CancellationToken ct) => Task.FromResult(_estado));

        _servicio = new(_repositorioMock.Object, _relacionesMock.Object);
    }

    #region Búsqueda de Momentos

    [Fact]
    public async Task QueSeNormaliceLaBusquedaYSePuedaContinuarLaPagina()
    {
        //Arrange
        var pagina1 = new List<MomentoResumenDto> { new() { Id = 1 }, new() { Id = 2 }, new() { Id = 3 } };
        var pagina2 = new List<MomentoResumenDto>();
        using var cancelacion = new CancellationTokenSource();

        _repositorioMock
            .Setup(r => r.Buscar("méxico", "mexico", null, 3, cancelacion.Token))
            .ReturnsAsync(pagina1);

        _repositorioMock
            .Setup(r => r.Buscar("méxico", "mexico", 2, 3, cancelacion.Token))
            .ReturnsAsync(pagina2);

        //Act
        var primera = await _servicio.Buscar("  MÉXICO  ", null, 2, cancelacion.Token);
        var segunda = await _servicio.Buscar("méxico", primera.SiguienteCursor, 2, cancelacion.Token);

        //Assert
        Assert.Equal(new[] { 1, 2 }, primera.Items.Select(m => m.Id));
        Assert.True(primera.HayMas);
        Assert.False(segunda.HayMas);
        _repositorioMock.Verify(r => r.Buscar("méxico", "mexico", null, 3, cancelacion.Token), Times.Once);
        _repositorioMock.Verify(r => r.Buscar("méxico", "mexico", 2, 3, cancelacion.Token), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task QueNoSeBusquenMomentosConLimitesInvalidos(int limite)
    {
        //Arrange
        var texto = "Mundial";

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Buscar(texto, null, limite, CancellationToken.None));

        //Assert
        _repositorioMock.Verify(r => r.Buscar(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNoSeBusquenTextosDemasiadoLargos()
    {
        //Arrange
        var texto = new string('a', 201);

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Buscar(texto, null, 20, CancellationToken.None));

        //Assert
        _repositorioMock.Verify(r => r.Buscar(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("basura")]
    [InlineData("bnVsbA==")]
    public async Task QueNoSeBusqueConUnCursorMalformado(string cursor)
    {
        //Arrange
        var texto = "Mundial";

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Buscar(texto, cursor, 20, CancellationToken.None));

        //Assert
        _repositorioMock.Verify(r => r.Buscar(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNoSeReutiliceElCursorEnOtraBusqueda()
    {
        //Arrange
        var pagina1 = new List<MomentoResumenDto> { new() { Id = 1 }, new() { Id = 2 } };
        _repositorioMock
            .Setup(r => r.Buscar("mundial", "mundial", null, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagina1);

        var primera = await _servicio.Buscar("Mundial", null, 1, CancellationToken.None);

        //Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Buscar("Otro", primera.SiguienteCursor, 1, CancellationToken.None));
    }

    #endregion

    #region Detalle y Publicaciones del Momento

    [Fact]
    public async Task QueSeDevuelvaNotFoundSiElMomentoNoExiste()
    {
        //Arrange
        _detalle = null;

        //Act
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerDetalle(99, CancellationToken.None));

        //Assert
        _repositorioMock.Verify(r => r.ObtenerDetalle(99, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueElCursorDePublicacionesConserveFechaIdYContexto()
    {
        //Arrange
        var fecha = new DateTime(1986, 6, 29, 12, 0, 0, DateTimeKind.Utc);
        var publicaciones1 = new List<PublicacionResumenDto> { new() { Id = 12, Fecha = fecha }, new() { Id = 11, Fecha = fecha } };
        _repositorioMock
            .Setup(r => r.ObtenerPublicaciones(1, true, null, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(publicaciones1);

        CursorExploracionMomentoDto? cursorSegundaLlamada = null;
        _repositorioMock
            .Setup(r => r.ObtenerPublicaciones(1, true, It.IsNotNull<CursorExploracionMomentoDto>(), 2, It.IsAny<CancellationToken>()))
            .Callback<int, bool, CursorExploracionMomentoDto?, int, CancellationToken>((_, _, cursor, _, _) => cursorSegundaLlamada = cursor)
            .ReturnsAsync(new List<PublicacionResumenDto>());

        //Act
        var primera = await _servicio.ObtenerPublicaciones(1, true, null, 1, CancellationToken.None);
        await _servicio.ObtenerPublicaciones(1, true, primera.SiguienteCursor, 1, CancellationToken.None);

        //Assert
        Assert.Equal(12, Assert.Single(primera.Items).Id);
        Assert.NotNull(cursorSegundaLlamada);
        Assert.Equal(fecha, cursorSegundaLlamada.Fecha);
        Assert.Equal(12, cursorSegundaLlamada.Id);
        Assert.Equal(1, cursorSegundaLlamada.Ambito);
        Assert.True(cursorSegundaLlamada.Descendientes);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    public async Task QueNoSeReutiliceElCursorDePublicacionesConOtroContexto(int id, bool descendientes)
    {
        //Arrange
        var fecha = DateTime.UtcNow;
        var publicaciones = new List<PublicacionResumenDto> { new() { Id = 12, Fecha = fecha }, new() { Id = 11, Fecha = fecha } };
        _repositorioMock
            .Setup(r => r.ObtenerPublicaciones(1, true, null, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(publicaciones);

        var primera = await _servicio.ObtenerPublicaciones(1, true, null, 1, CancellationToken.None);

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerPublicaciones(id, descendientes, primera.SiguienteCursor, 1, CancellationToken.None));

        //Assert
        _repositorioMock.Verify(r => r.ObtenerPublicaciones(id, descendientes, It.IsAny<CursorExploracionMomentoDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Estado y Corrección de Momentos

    [Fact]
    public async Task QueSeObtengaElEstadoDelOriginalCuandoSeConsultaUnRepost()
    {
        //Arrange
        _original = 10;

        //Act
        var result = await _servicio.ObtenerEstado(25, 7, CancellationToken.None);

        //Assert
        Assert.Same(_estado, result);
        _repositorioMock.Verify(r => r.ObtenerEstado(10, 7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNoSeObtengaEstadoSiElRecuerdoNoEstaDisponible()
    {
        //Arrange
        _original = null;

        //Act
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerEstado(99, null, CancellationToken.None));

        //Assert
        _repositorioMock.Verify(r => r.ObtenerEstado(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueSePuedaGuardarUnaSeleccionVaciaYDesactivarLaAutomatizacion()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();

        //Act
        var result = await _servicio.Corregir(10, 7, Array.Empty<int>(), false, 3, cancelacion.Token);

        //Assert
        Assert.Same(_estado, result);
        _repositorioMock.Verify(r => r.Corregir(
            10,
            7,
            It.Is<IReadOnlyCollection<int>>(c => c.Count == 0),
            false,
            3,
            cancelacion.Token), Times.Once);
    }

    [Theory]
    [InlineData(0, 7, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(10, 7, -1)]
    public async Task QueNoSeCorrijaConIdentificadoresOVersionInvalidos(int id, int usuarioId, long version)
    {
        //Arrange
        var ids = new[] { 1 };

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Corregir(id, usuarioId, ids, true, version, CancellationToken.None));

        //Assert
        _repositorioMock.Verify(r => r.Corregir(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(new[] { 1, 1 })]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { -1 })]
    public async Task QueNoSeCorrijaConMomentosInvalidosORepetidos(int[] ids)
    {
        //Arrange
        var version = 0L;

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Corregir(10, 7, ids, true, version, CancellationToken.None));

        //Assert
        _repositorioMock.Verify(r => r.Corregir(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNoSeCorrijaConMasDeCincuentaMomentos()
    {
        //Arrange
        var ids = Enumerable.Range(1, 51).ToArray();

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Corregir(10, 7, ids, true, 0, CancellationToken.None));

        //Assert
        _repositorioMock.Verify(r => r.Corregir(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
