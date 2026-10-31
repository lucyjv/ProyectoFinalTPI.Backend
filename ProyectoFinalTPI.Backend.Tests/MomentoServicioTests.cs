using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class MomentoServicioTests
{
    private readonly Mock<IMomentoRepositorio> _momentoRepositorioMock = new();
    private readonly Mock<IPublicacionRepositorio> _publicacionRepositorioMock = new();
    private readonly MomentoServicio _servicio;
    private Publicacion? _publicacion = new() { Id = 10, UsuarioId = 7 };

    public MomentoServicioTests()
    {
        _publicacionRepositorioMock
            .Setup(p => p.ObtenerPorId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int id, CancellationToken ct) => Task.FromResult<Publicacion?>(_publicacion));

        _momentoRepositorioMock
            .Setup(m => m.ObtenerPorId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken ct) => new Momento { Id = id, Nombre = "Mundial" });

        _momentoRepositorioMock
            .Setup(m => m.Crear(It.IsAny<Momento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        _momentoRepositorioMock
            .Setup(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _servicio = new MomentoServicio(_momentoRepositorioMock.Object, _publicacionRepositorioMock.Object);
    }

    #region Creación de Momentos y Validaciones

    [Fact]
    public async Task QueSePuedaCrearUnMomentoConNombreDescripcionYFechas()
    {
        //Arrange
        Momento? momentoCreado = null;
        _momentoRepositorioMock
            .Setup(m => m.Crear(It.IsAny<Momento>(), It.IsAny<CancellationToken>()))
            .Callback<Momento, CancellationToken>((m, _) => momentoCreado = m)
            .ReturnsAsync(25);

        var momento = new Momento
        {
            Nombre = "  Mundial de México 1986  ",
            Descripcion = "  Un acontecimiento colectivo  ",
            FechaInicio = new DateOnly(1986, 5, 31),
            FechaFin = new DateOnly(1986, 6, 29),
            MomentoPadreId = 2
        };
        var desde = DateTime.UtcNow;
        using var cancelacion = new CancellationTokenSource();

        //Act
        var result = await _servicio.Crear(momento, cancelacion.Token);

        //Assert
        Assert.Equal(25, result);
        Assert.NotNull(momentoCreado);
        Assert.Equal("Mundial de México 1986", momentoCreado.Nombre);
        Assert.Equal("Un acontecimiento colectivo", momentoCreado.Descripcion);
        Assert.Equal(momento.FechaInicio, momentoCreado.FechaInicio);
        Assert.Equal(momento.FechaFin, momentoCreado.FechaFin);
        Assert.Equal(2, momentoCreado.MomentoPadreId);
        Assert.InRange(momentoCreado.FechaCreacion, desde, DateTime.UtcNow);
        _momentoRepositorioMock.Verify(m => m.Crear(It.IsAny<Momento>(), cancelacion.Token), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task QueNoSeCreeUnMomentoSinNombre(string? nombre)
    {
        //Arrange
        var momento = new Momento { Nombre = nombre! };

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Crear(momento));

        //Assert
        Assert.Contains("nombre", result.Message);
        _momentoRepositorioMock.Verify(m => m.Crear(It.IsAny<Momento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueNoSeCreeUnMomentoConTextosDemasiadoLargos(bool nombre)
    {
        //Arrange
        var momento = new Momento
        {
            Nombre = nombre ? new string('a', 201) : "Mundial",
            Descripcion = nombre ? "" : new string('a', 2001)
        };

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Crear(momento));

        //Assert
        _momentoRepositorioMock.Verify(m => m.Crear(It.IsAny<Momento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNoSeCreeUnMomentoConFechasInvertidas()
    {
        //Arrange
        var momento = new Momento
        {
            Nombre = "Mundial",
            FechaInicio = new DateOnly(1986, 6, 29),
            FechaFin = new DateOnly(1986, 5, 31)
        };

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Crear(momento));

        //Assert
        Assert.Contains("fecha", result.Message);
        _momentoRepositorioMock.Verify(m => m.Crear(It.IsAny<Momento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueSePermitanFechasIgualesOAusentes()
    {
        //Arrange
        Momento? ultimoMomentoCreado = null;
        _momentoRepositorioMock
            .Setup(m => m.Crear(It.IsAny<Momento>(), It.IsAny<CancellationToken>()))
            .Callback<Momento, CancellationToken>((m, _) => ultimoMomentoCreado = m)
            .ReturnsAsync(25);

        var fecha = new DateOnly(1986, 6, 29);
        var momento = new Momento { Nombre = "La final", FechaInicio = fecha, FechaFin = fecha };

        //Act
        await _servicio.Crear(momento);
        await _servicio.Crear(new Momento { Nombre = "Sin fecha precisa" });

        //Assert
        Assert.NotNull(ultimoMomentoCreado);
        Assert.Null(ultimoMomentoCreado.FechaInicio);
        Assert.Null(ultimoMomentoCreado.FechaFin);
        _momentoRepositorioMock.Verify(m => m.Crear(It.IsAny<Momento>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task QueNoSeCreenEntidadesAnidadasSaltandoLasValidaciones()
    {
        //Arrange
        var momento = new Momento { Nombre = "Mundial", Alias = new List<MomentoAlias> { new() { Texto = "" } } };

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Crear(momento));

        //Assert
        Assert.Contains("anidadas", result.Message);
        _momentoRepositorioMock.Verify(m => m.Crear(It.IsAny<Momento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Modificación de Jerarquía y Alias

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    public async Task QueNoSeCambieElPadreConIdsInvalidosOAlMismoMomento(int id, int padreId)
    {
        //Arrange
        var padre = (int?)padreId;

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.CambiarPadre(id, padre));

        //Assert
        _momentoRepositorioMock.Verify(m => m.CambiarPadre(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(null)]
    public async Task QueSePuedaSolicitarElCambioOLaSeparacionDelPadre(int? padreId)
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();

        //Act
        await _servicio.CambiarPadre(1, padreId, cancelacion.Token);

        //Assert
        _momentoRepositorioMock.Verify(m => m.CambiarPadre(1, padreId, cancelacion.Token), Times.Once);
    }

    [Fact]
    public async Task QueSeNormalicenTildesMayusculasYEspaciosDeLosAlias()
    {
        //Arrange
        MomentoAlias? aliasAgregado = null;
        _momentoRepositorioMock
            .Setup(m => m.AgregarAlias(It.IsAny<MomentoAlias>(), It.IsAny<CancellationToken>()))
            .Callback<MomentoAlias, CancellationToken>((a, _) => aliasAgregado = a)
            .Returns(Task.CompletedTask);
        var texto = "  MÉXICO   86  ";

        //Act
        await _servicio.AgregarAlias(1, texto);

        //Assert
        Assert.NotNull(aliasAgregado);
        Assert.Equal(1, aliasAgregado.MomentoId);
        Assert.Equal("MÉXICO   86", aliasAgregado.Texto);
        Assert.Equal("mexico 86", aliasAgregado.TextoNormalizado);
        _momentoRepositorioMock.Verify(m => m.AgregarAlias(It.IsAny<MomentoAlias>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task QueNoSeAgregueUnAliasVacio(string? texto)
    {
        //Arrange
        var id = 1;

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.AgregarAlias(id, texto!));

        //Assert
        _momentoRepositorioMock.Verify(m => m.AgregarAlias(It.IsAny<MomentoAlias>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Asociación Automática de Momentos

    [Fact]
    public async Task QueSePuedaAsociarAutomaticamenteUnRecuerdoAUnMomento()
    {
        //Arrange
        PublicacionMomento? asociacionGuardada = null;
        _momentoRepositorioMock
            .Setup(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), It.IsAny<CancellationToken>()))
            .Callback<PublicacionMomento, CancellationToken>((a, _) => asociacionGuardada = a)
            .ReturnsAsync(true);
        using var cancelacion = new CancellationTokenSource();

        //Act
        var result = await _servicio.AsociarAutomaticamente(10, 1, 0.95, "  v1  ", cancelacion.Token);

        //Assert
        Assert.True(result);
        Assert.NotNull(asociacionGuardada);
        Assert.Equal(10, asociacionGuardada.PublicacionId);
        Assert.Equal(1, asociacionGuardada.MomentoId);
        Assert.Equal(OrigenAsociacionMomento.Automatico, asociacionGuardada.Origen);
        Assert.Equal(EstadoAsociacionMomento.Activo, asociacionGuardada.Estado);
        Assert.Equal(0.95, asociacionGuardada.Confianza);
        Assert.Equal("v1", asociacionGuardada.VersionAnalisis);
        _momentoRepositorioMock.Verify(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), cancelacion.Token), Times.Once);
    }

    [Fact]
    public async Task QueSeInformeCuandoUnaDecisionDelAutorImpideLaAsociacionAutomatica()
    {
        //Arrange
        _momentoRepositorioMock
            .Setup(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        //Act
        var result = await _servicio.AsociarAutomaticamente(10, 1, 0.9, "v1");

        //Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task QueNoSeAsocieAutomaticamenteConConfianzaInvalida(double confianza)
    {
        //Arrange
        var version = "v1";

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.AsociarAutomaticamente(10, 1, confianza, version));

        //Assert
        _momentoRepositorioMock.Verify(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), It.IsAny<CancellationToken>()), Times.Never);
        _publicacionRepositorioMock.Verify(p => p.ObtenerPorId(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task QueSeAceptenLosLimitesDeConfianza(double confianza)
    {
        //Arrange
        PublicacionMomento? asociacionGuardada = null;
        _momentoRepositorioMock
            .Setup(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), It.IsAny<CancellationToken>()))
            .Callback<PublicacionMomento, CancellationToken>((a, _) => asociacionGuardada = a)
            .ReturnsAsync(true);
        var version = "v1";

        //Act
        var result = await _servicio.AsociarAutomaticamente(10, 1, confianza, version);

        //Assert
        Assert.True(result);
        Assert.NotNull(asociacionGuardada);
        Assert.Equal(confianza, asociacionGuardada.Confianza);
    }

    [Fact]
    public async Task QueNoSeAsocieUnRecuerdoAUnMomentoInexistente()
    {
        //Arrange
        _momentoRepositorioMock
            .Setup(m => m.ObtenerPorId(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Momento?)null);

        //Act
        var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.AsociarAutomaticamente(10, 99, 0.9, "v1"));

        //Assert
        Assert.Contains("momento", result.Message);
        _momentoRepositorioMock.Verify(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueNoSeAsocieUnRecuerdoInexistenteUOculto(bool oculto)
    {
        //Arrange
        _publicacion = oculto ? new Publicacion { Id = 10, EstaOculto = true } : null;

        //Act
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.AsociarAutomaticamente(10, 1, 0.9, "v1"));

        //Assert
        _momentoRepositorioMock.Verify(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNoSeAsocieUnRepostComoSiFueraUnRecuerdoNuevo()
    {
        //Arrange
        _publicacion!.PublicacionOriginalId = 5;

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.AsociarAutomaticamente(10, 1, 0.9, "v1"));

        //Assert
        Assert.Contains("original", result.Message);
        _momentoRepositorioMock.Verify(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Asociación por Autor y Consultas

    [Fact]
    public async Task QueElAutorPuedaAsociarSuRecuerdoSinConfianzaDeIA()
    {
        //Arrange
        PublicacionMomento? asociacionGuardada = null;
        _momentoRepositorioMock
            .Setup(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), It.IsAny<CancellationToken>()))
            .Callback<PublicacionMomento, CancellationToken>((a, _) => asociacionGuardada = a)
            .ReturnsAsync(true);
        var usuarioId = 7;

        //Act
        await _servicio.AsociarPorAutor(10, 1, usuarioId);

        //Assert
        Assert.NotNull(asociacionGuardada);
        Assert.Equal(OrigenAsociacionMomento.Autor, asociacionGuardada.Origen);
        Assert.Equal(EstadoAsociacionMomento.Activo, asociacionGuardada.Estado);
        Assert.Null(asociacionGuardada.Confianza);
        Assert.Null(asociacionGuardada.VersionAnalisis);
    }

    [Fact]
    public async Task QueOtroUsuarioNoPuedaAsociarElRecuerdoDelAutor()
    {
        //Arrange
        var otroUsuario = 99;

        //Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _servicio.AsociarPorAutor(10, 1, otroUsuario));

        //Assert
        _momentoRepositorioMock.Verify(m => m.GuardarAsociacion(It.IsAny<PublicacionMomento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueSePuedanConsultarLosMomentosDeUnRecuerdoVisible()
    {
        //Arrange
        var momentos = new List<Momento> { new() { Id = 1, Nombre = "Mundial" } };
        _momentoRepositorioMock
            .Setup(m => m.ObtenerMomentosPublicacion(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(momentos);

        //Act
        var result = await _servicio.ObtenerMomentosPublicacion(10);

        //Assert
        Assert.Equal(1, Assert.Single(result).Id);
        _momentoRepositorioMock.Verify(m => m.ObtenerMomentosPublicacion(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
