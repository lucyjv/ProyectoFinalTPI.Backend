using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class MomentoServicioTests
{
    private readonly MomentoRepositorioFake _repositorio = new();
    private readonly Mock<IPublicacionRepositorio> _publicaciones = new(MockBehavior.Strict);
    private readonly MomentoServicio _servicio;
    private Publicacion? _publicacion = new() { Id = 10, UsuarioId = 7 };

    public MomentoServicioTests()
    {
        _publicaciones.Setup(p => p.ObtenerPorId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int id, CancellationToken ct) => Task.FromResult<Publicacion?>(_publicacion));
        _servicio = new MomentoServicio(_repositorio, _publicaciones.Object);
    }

    [Fact]
    public async Task QueSePuedaCrearUnMomentoConNombreDescripcionYFechas()
    {
        //Arrange
        var momento = new Momento
        {
            Nombre = "  Mundial de México 1986  ", Descripcion = "  Un acontecimiento colectivo  ",
            FechaInicio = new DateOnly(1986, 5, 31), FechaFin = new DateOnly(1986, 6, 29), MomentoPadreId = 2
        };
        var desde = DateTime.UtcNow;
        using var cancelacion = new CancellationTokenSource();

        //Act
        var result = await _servicio.Crear(momento, cancelacion.Token);

        //Assert
        Assert.Equal(25, result);
        Assert.NotNull(_repositorio.Creado);
        Assert.Equal("Mundial de México 1986", _repositorio.Creado.Nombre);
        Assert.Equal("Un acontecimiento colectivo", _repositorio.Creado.Descripcion);
        Assert.Equal(momento.FechaInicio, _repositorio.Creado.FechaInicio);
        Assert.Equal(momento.FechaFin, _repositorio.Creado.FechaFin);
        Assert.Equal(2, _repositorio.Creado.MomentoPadreId);
        Assert.InRange(_repositorio.Creado.FechaCreacion, desde, DateTime.UtcNow);
        Assert.Equal(cancelacion.Token, _repositorio.TokenRecibido);
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
        Assert.Null(_repositorio.Creado);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueNoSeCreeUnMomentoConTextosDemasiadoLargos(bool nombre)
    {
        //Arrange
        var momento = new Momento { Nombre = nombre ? new string('a', 201) : "Mundial", Descripcion = nombre ? "" : new string('a', 2001) };

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Crear(momento));

        //Assert
        Assert.Null(_repositorio.Creado);
    }

    [Fact]
    public async Task QueNoSeCreeUnMomentoConFechasInvertidas()
    {
        //Arrange
        var momento = new Momento { Nombre = "Mundial", FechaInicio = new DateOnly(1986, 6, 29), FechaFin = new DateOnly(1986, 5, 31) };

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Crear(momento));

        //Assert
        Assert.Contains("fecha", result.Message);
        Assert.Null(_repositorio.Creado);
    }

    [Fact]
    public async Task QueSePermitanFechasIgualesOAusentes()
    {
        //Arrange
        var fecha = new DateOnly(1986, 6, 29);
        var momento = new Momento { Nombre = "La final", FechaInicio = fecha, FechaFin = fecha };

        //Act
        await _servicio.Crear(momento);
        await _servicio.Crear(new Momento { Nombre = "Sin fecha precisa" });

        //Assert
        Assert.NotNull(_repositorio.Creado);
        Assert.Null(_repositorio.Creado.FechaInicio);
        Assert.Null(_repositorio.Creado.FechaFin);
        Assert.Equal(2, _repositorio.Creaciones);
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
        Assert.Null(_repositorio.Creado);
    }

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
        Assert.Null(_repositorio.IdCambioPadre);
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
        Assert.Equal(1, _repositorio.IdCambioPadre);
        Assert.Equal(padreId, _repositorio.PadreSolicitado);
        Assert.Equal(cancelacion.Token, _repositorio.TokenRecibido);
    }

    [Fact]
    public async Task QueSeNormalicenTildesMayusculasYEspaciosDeLosAlias()
    {
        //Arrange
        var texto = "  MÉXICO   86  ";

        //Act
        await _servicio.AgregarAlias(1, texto);

        //Assert
        Assert.NotNull(_repositorio.AliasAgregado);
        Assert.Equal(1, _repositorio.AliasAgregado.MomentoId);
        Assert.Equal("MÉXICO   86", _repositorio.AliasAgregado.Texto);
        Assert.Equal("mexico 86", _repositorio.AliasAgregado.TextoNormalizado);
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
        Assert.Null(_repositorio.AliasAgregado);
    }

    [Fact]
    public async Task QueSePuedaAsociarAutomaticamenteUnRecuerdoAUnMomento()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();

        //Act
        var result = await _servicio.AsociarAutomaticamente(10, 1, 0.95, "  v1  ", cancelacion.Token);

        //Assert
        Assert.True(result);
        Assert.NotNull(_repositorio.AsociacionGuardada);
        Assert.Equal(10, _repositorio.AsociacionGuardada.PublicacionId);
        Assert.Equal(1, _repositorio.AsociacionGuardada.MomentoId);
        Assert.Equal(OrigenAsociacionMomento.Automatico, _repositorio.AsociacionGuardada.Origen);
        Assert.Equal(EstadoAsociacionMomento.Activo, _repositorio.AsociacionGuardada.Estado);
        Assert.Equal(0.95, _repositorio.AsociacionGuardada.Confianza);
        Assert.Equal("v1", _repositorio.AsociacionGuardada.VersionAnalisis);
        Assert.Equal(cancelacion.Token, _repositorio.TokenRecibido);
    }

    [Fact]
    public async Task QueSeInformeCuandoUnaDecisionDelAutorImpideLaAsociacionAutomatica()
    {
        //Arrange
        _repositorio.GuardadoPermitido = false;

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
        Assert.Null(_repositorio.AsociacionGuardada);
        _publicaciones.Verify(p => p.ObtenerPorId(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task QueSeAceptenLosLimitesDeConfianza(double confianza)
    {
        //Arrange
        var version = "v1";

        //Act
        var result = await _servicio.AsociarAutomaticamente(10, 1, confianza, version);

        //Assert
        Assert.True(result);
        Assert.Equal(confianza, _repositorio.AsociacionGuardada!.Confianza);
    }

    [Fact]
    public async Task QueNoSeAsocieUnRecuerdoAUnMomentoInexistente()
    {
        //Arrange
        _repositorio.MomentoDisponible = null;

        //Act
        var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.AsociarAutomaticamente(10, 99, 0.9, "v1"));

        //Assert
        Assert.Contains("momento", result.Message);
        Assert.Null(_repositorio.AsociacionGuardada);
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
        Assert.Null(_repositorio.AsociacionGuardada);
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
        Assert.Null(_repositorio.AsociacionGuardada);
    }

    [Fact]
    public async Task QueElAutorPuedaAsociarSuRecuerdoSinConfianzaDeIA()
    {
        //Arrange
        var usuarioId = 7;

        //Act
        await _servicio.AsociarPorAutor(10, 1, usuarioId);

        //Assert
        var result = _repositorio.AsociacionGuardada;
        Assert.NotNull(result);
        Assert.Equal(OrigenAsociacionMomento.Autor, result.Origen);
        Assert.Equal(EstadoAsociacionMomento.Activo, result.Estado);
        Assert.Null(result.Confianza);
        Assert.Null(result.VersionAnalisis);
    }

    [Fact]
    public async Task QueOtroUsuarioNoPuedaAsociarElRecuerdoDelAutor()
    {
        //Arrange
        var otroUsuario = 99;

        //Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _servicio.AsociarPorAutor(10, 1, otroUsuario));

        //Assert
        Assert.Null(_repositorio.AsociacionGuardada);
    }

    [Fact]
    public async Task QueSePuedanConsultarLosMomentosDeUnRecuerdoVisible()
    {
        //Arrange
        _repositorio.MomentosPublicacion.Add(new Momento { Id = 1, Nombre = "Mundial" });

        //Act
        var result = await _servicio.ObtenerMomentosPublicacion(10);

        //Assert
        Assert.Equal(1, Assert.Single(result).Id);
        Assert.Equal(10, _repositorio.PublicacionConsultada);
    }

    private sealed class MomentoRepositorioFake : IMomentoRepositorio
    {
        public Momento? Creado, MomentoDisponible = new() { Id = 1, Nombre = "Mundial" };
        public int Creaciones;
        public int? IdCambioPadre, PadreSolicitado, PublicacionConsultada;
        public MomentoAlias? AliasAgregado;
        public PublicacionMomento? AsociacionGuardada;
        public bool GuardadoPermitido = true;
        public CancellationToken TokenRecibido;
        public List<Momento> MomentosPublicacion = new();

        public Task<int> Crear(Momento momento, CancellationToken ct = default)
        {
            Creado = momento; Creaciones++; TokenRecibido = ct;
            return Task.FromResult(25);
        }
        public Task<Momento?> ObtenerPorId(int id, CancellationToken ct = default) => Task.FromResult(MomentoDisponible);
        public Task<List<Momento>> ObtenerHijos(int id, CancellationToken ct = default) => Task.FromResult(new List<Momento>());
        public Task CambiarPadre(int id, int? padreId, CancellationToken ct = default)
        {
            IdCambioPadre = id; PadreSolicitado = padreId; TokenRecibido = ct;
            return Task.CompletedTask;
        }
        public Task AgregarAlias(MomentoAlias alias, CancellationToken ct = default)
        {
            AliasAgregado = alias; TokenRecibido = ct;
            return Task.CompletedTask;
        }
        public Task<PublicacionMomento?> ObtenerAsociacion(int publicacionId, int momentoId, CancellationToken ct = default) => Task.FromResult<PublicacionMomento?>(null);
        public Task<bool> GuardarAsociacion(PublicacionMomento asociacion, CancellationToken ct = default)
        {
            AsociacionGuardada = asociacion; TokenRecibido = ct;
            return Task.FromResult(GuardadoPermitido);
        }
        public Task<List<Momento>> ObtenerMomentosPublicacion(int publicacionId, CancellationToken ct = default)
        {
            PublicacionConsultada = publicacionId;
            return Task.FromResult(MomentosPublicacion);
        }
    }
}
