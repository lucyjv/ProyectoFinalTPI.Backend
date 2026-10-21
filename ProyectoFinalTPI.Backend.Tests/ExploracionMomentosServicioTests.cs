using Moq;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ExploracionMomentosServicioTests
{
    private readonly RepositorioFake _repositorio = new();
    private readonly Mock<IRelacionRecuerdosRepositorio> _relaciones = new(MockBehavior.Strict);
    private readonly ExploracionMomentosServicio _servicio;
    private int? _original = 10;

    public ExploracionMomentosServicioTests()
    {
        _relaciones.Setup(r => r.ObtenerOriginalVisible(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int id, CancellationToken ct) => Task.FromResult(_original));
        _servicio = new(_repositorio, _relaciones.Object);
    }

    [Fact]
    public async Task QueSeNormaliceLaBusquedaYSePuedaContinuarLaPagina()
    {
        //Arrange
        _repositorio.ResultadosBusqueda.AddRange(new[] { new MomentoResumenDto { Id = 1 }, new MomentoResumenDto { Id = 2 }, new MomentoResumenDto { Id = 3 } });
        using var cancelacion = new CancellationTokenSource();

        //Act
        var primera = await _servicio.Buscar("  MÉXICO  ", null, 2, cancelacion.Token);
        _repositorio.ResultadosBusqueda.Clear();
        var segunda = await _servicio.Buscar("méxico", primera.SiguienteCursor, 2, cancelacion.Token);

        //Assert
        Assert.Equal(new[] { 1, 2 }, primera.Items.Select(m => m.Id));
        Assert.True(primera.HayMas);
        Assert.False(segunda.HayMas);
        Assert.Equal("méxico", _repositorio.Texto);
        Assert.Equal("mexico", _repositorio.Normalizado);
        Assert.Equal(2, _repositorio.DespuesId);
        Assert.Equal(3, _repositorio.Limite);
        Assert.Equal(cancelacion.Token, _repositorio.Token);
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
        Assert.Null(_repositorio.Texto);
    }

    [Fact]
    public async Task QueNoSeBusquenTextosDemasiadoLargos()
    {
        //Arrange
        var texto = new string('a', 201);

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Buscar(texto, null, 20, CancellationToken.None));

        //Assert
        Assert.Null(_repositorio.Texto);
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
        Assert.Null(_repositorio.Texto);
    }

    [Fact]
    public async Task QueNoSeReutiliceElCursorEnOtraBusqueda()
    {
        //Arrange
        _repositorio.ResultadosBusqueda.AddRange(new[] { new MomentoResumenDto { Id = 1 }, new MomentoResumenDto { Id = 2 } });
        var primera = await _servicio.Buscar("Mundial", null, 1, CancellationToken.None);

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Buscar("Otro", primera.SiguienteCursor, 1, CancellationToken.None));

        //Assert
        Assert.Equal("mundial", _repositorio.Texto);
    }

    [Fact]
    public async Task QueSeDevuelvaNotFoundSiElMomentoNoExiste()
    {
        //Arrange
        _repositorio.Detalle = null;

        //Act
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerDetalle(99, CancellationToken.None));

        //Assert
        Assert.Equal(99, _repositorio.IdConsultado);
    }

    [Fact]
    public async Task QueElCursorDePublicacionesConserveFechaIdYContexto()
    {
        //Arrange
        var fecha = new DateTime(1986, 6, 29, 12, 0, 0, DateTimeKind.Utc);
        _repositorio.Publicaciones.AddRange(new[] { new PublicacionResumenDto { Id = 12, Fecha = fecha }, new PublicacionResumenDto { Id = 11, Fecha = fecha } });

        //Act
        var primera = await _servicio.ObtenerPublicaciones(1, true, null, 1, CancellationToken.None);
        _repositorio.Publicaciones.Clear();
        await _servicio.ObtenerPublicaciones(1, true, primera.SiguienteCursor, 1, CancellationToken.None);

        //Assert
        Assert.Equal(12, Assert.Single(primera.Items).Id);
        Assert.NotNull(_repositorio.CursorPublicaciones);
        Assert.Equal(fecha, _repositorio.CursorPublicaciones.Fecha);
        Assert.Equal(12, _repositorio.CursorPublicaciones.Id);
        Assert.Equal(1, _repositorio.CursorPublicaciones.Ambito);
        Assert.True(_repositorio.CursorPublicaciones.Descendientes);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    public async Task QueNoSeReutiliceElCursorDePublicacionesConOtroContexto(int id, bool descendientes)
    {
        //Arrange
        var fecha = DateTime.UtcNow;
        _repositorio.Publicaciones.AddRange(new[] { new PublicacionResumenDto { Id = 12, Fecha = fecha }, new PublicacionResumenDto { Id = 11, Fecha = fecha } });
        var primera = await _servicio.ObtenerPublicaciones(1, true, null, 1, CancellationToken.None);

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerPublicaciones(id, descendientes, primera.SiguienteCursor, 1, CancellationToken.None));

        //Assert
        Assert.Null(_repositorio.CursorPublicaciones);
    }

    [Fact]
    public async Task QueSeObtengaElEstadoDelOriginalCuandoSeConsultaUnRepost()
    {
        //Arrange
        _original = 10;

        //Act
        var result = await _servicio.ObtenerEstado(25, 7, CancellationToken.None);

        //Assert
        Assert.Same(_repositorio.Estado, result);
        Assert.Equal(10, _repositorio.IdConsultado);
        Assert.Equal(7, _repositorio.UsuarioConsultado);
    }

    [Fact]
    public async Task QueNoSeObtengaEstadoSiElRecuerdoNoEstaDisponible()
    {
        //Arrange
        _original = null;

        //Act
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerEstado(99, null, CancellationToken.None));

        //Assert
        Assert.Null(_repositorio.IdConsultado);
    }

    [Fact]
    public async Task QueSePuedaGuardarUnaSeleccionVaciaYDesactivarLaAutomatizacion()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();

        //Act
        var result = await _servicio.Corregir(10, 7, Array.Empty<int>(), false, 3, cancelacion.Token);

        //Assert
        Assert.Same(_repositorio.Estado, result);
        Assert.Equal(10, _repositorio.IdConsultado);
        Assert.Equal(7, _repositorio.UsuarioConsultado);
        Assert.Empty(_repositorio.IdsSeleccionados!);
        Assert.False(_repositorio.Permitir);
        Assert.Equal(3, _repositorio.Version);
        Assert.Equal(cancelacion.Token, _repositorio.Token);
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
        Assert.Null(_repositorio.IdsSeleccionados);
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
        Assert.Null(_repositorio.IdsSeleccionados);
    }

    [Fact]
    public async Task QueNoSeCorrijaConMasDeCincuentaMomentos()
    {
        //Arrange
        var ids = Enumerable.Range(1, 51).ToArray();

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Corregir(10, 7, ids, true, 0, CancellationToken.None));

        //Assert
        Assert.Null(_repositorio.IdsSeleccionados);
    }

    private sealed class RepositorioFake : IExploracionMomentosRepositorio
    {
        public string? Texto, Normalizado;
        public int? IdConsultado, UsuarioConsultado, DespuesId;
        public int Limite;
        public long Version;
        public bool Permitir;
        public CancellationToken Token;
        public IReadOnlyCollection<int>? IdsSeleccionados;
        public MomentoDetalleDto? Detalle = new() { Id = 1 };
        public MomentosPublicacionDto Estado = new() { PublicacionId = 10 };
        public List<MomentoResumenDto> ResultadosBusqueda = new();
        public List<PublicacionResumenDto> Publicaciones = new();
        public CursorExploracionMomentoDto? CursorPublicaciones;

        public Task<List<MomentoResumenDto>> Buscar(string texto, string normalizado, int? despuesId, int limite, CancellationToken ct)
        {
            Texto = texto; Normalizado = normalizado; DespuesId = despuesId; Limite = limite; Token = ct;
            return Task.FromResult(ResultadosBusqueda.ToList());
        }
        public Task<MomentoDetalleDto?> ObtenerDetalle(int id, CancellationToken ct) { IdConsultado = id; return Task.FromResult(Detalle); }
        public Task<List<PublicacionResumenDto>> ObtenerPublicaciones(int id, bool descendientes, CursorExploracionMomentoDto? cursor, int limite, CancellationToken ct)
        { CursorPublicaciones = cursor; return Task.FromResult(Publicaciones.ToList()); }
        public Task<MomentosPublicacionDto?> ObtenerEstado(int id, int? usuarioId, CancellationToken ct)
        { IdConsultado = id; UsuarioConsultado = usuarioId; return Task.FromResult<MomentosPublicacionDto?>(Estado); }
        public Task<MomentosPublicacionDto> Corregir(int id, int usuarioId, IReadOnlyCollection<int> momentoIds, bool permitirAutomaticos, long version, CancellationToken ct)
        {
            IdConsultado = id; UsuarioConsultado = usuarioId; IdsSeleccionados = momentoIds; Permitir = permitirAutomaticos; Version = version; Token = ct;
            return Task.FromResult(Estado);
        }
    }
}
