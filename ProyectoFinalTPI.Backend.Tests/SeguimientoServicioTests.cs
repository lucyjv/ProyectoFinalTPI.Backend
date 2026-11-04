using Moq;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class SeguimientoServicioTests
{
    private readonly Mock<IUsuarioRepositorio> _usuarioRepositorioMock = new();
    private readonly Mock<IDriver> _driverMock = new();
    private readonly Mock<IAsyncSession> _sessionMock = new();
    private readonly Mock<IAsyncQueryRunner> _runnerMock = new();
    private readonly Mock<ILogroServicio> _logroServicioMock = new();
    private readonly Mock<INotificacionServicio> _notificacionServicioMock = new();
    private readonly SeguimientoServicio _servicio;

    public SeguimientoServicioTests()
    {
        _driverMock.Setup(d => d.AsyncSession()).Returns(_sessionMock.Object);

        _sessionMock
            .Setup(s => s.ExecuteWriteAsync(It.IsAny<Func<IAsyncQueryRunner, Task<IResultSummary>>>(), It.IsAny<Action<TransactionConfigBuilder>>()))
            .Returns<Func<IAsyncQueryRunner, Task<IResultSummary>>, Action<TransactionConfigBuilder>>((work, _) => work(_runnerMock.Object));

        _sessionMock
            .Setup(s => s.ExecuteReadAsync(It.IsAny<Func<IAsyncQueryRunner, Task<int>>>(), It.IsAny<Action<TransactionConfigBuilder>>()))
            .Returns<Func<IAsyncQueryRunner, Task<int>>, Action<TransactionConfigBuilder>>((work, _) => work(_runnerMock.Object));

        _sessionMock
            .Setup(s => s.ExecuteReadAsync(It.IsAny<Func<IAsyncQueryRunner, Task<List<int>>>>(), It.IsAny<Action<TransactionConfigBuilder>>()))
            .Returns<Func<IAsyncQueryRunner, Task<List<int>>>, Action<TransactionConfigBuilder>>((work, _) => work(_runnerMock.Object));

        _servicio = new SeguimientoServicio(
            _usuarioRepositorioMock.Object,
            _driverMock.Object,
            _logroServicioMock.Object,
            _notificacionServicioMock.Object);
    }

    #region Seguir Usuario

    [Fact]
    public async Task QueSeguirseASiMismoDevuelvaAutoSeguimientoSinConsultarUsuarios()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10);

        //Act
        var resultado = await _servicio.SeguirUsuario(10, 10);

        //Assert
        Assert.Equal(ResultadoSeguimiento.AutoSeguimiento, resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(10, 20)]
    [InlineData(20, 10)]
    public async Task QueSiFaltaCualquieraDeLosUsuariosDevuelvaUsuarioNoEncontradoAlSeguir(
        int idUsuarioExistente,
        int idUsuarioInexistente)
    {
        //Arrange
        ConfigurarUsuariosExistentes(idUsuarioExistente);

        //Act
        var resultado = await _servicio.SeguirUsuario(10, 20);

        //Assert
        Assert.Equal(ResultadoSeguimiento.UsuarioNoEncontrado, resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(idUsuarioExistente, It.IsAny<CancellationToken>()), Times.Once);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(idUsuarioInexistente, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueAlCrearRelacionExitosamenteSeNotifiqueYVerifiqueLogrosDevolviendoAplicado()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10, 20);
        ConfigurarNeo4jEscritura(relacionesCreadas: 1);
        ConfigurarNeo4jContadores(totalSeguidos: 2, totalSeguidores: 3);

        //Act
        var resultado = await _servicio.SeguirUsuario(10, 20);

        //Assert
        Assert.Equal(ResultadoSeguimiento.Aplicado, resultado);
        _notificacionServicioMock.Verify(n => n.NotificarNuevoSeguidorAsync(20, 10, It.IsAny<CancellationToken>()), Times.Once);
        _logroServicioMock.Verify(l => l.VerificarYDarLogro(It.IsAny<int>(), It.IsAny<LogroEnum>()), Times.Never);
    }

    [Fact]
    public async Task QueSiCreacionNoGeneraRelacionesDevuelvaSinCambiosSinNotificar()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10, 20);
        ConfigurarNeo4jEscritura(relacionesCreadas: 0);

        //Act
        var resultado = await _servicio.SeguirUsuario(10, 20);

        //Assert
        Assert.Equal(ResultadoSeguimiento.SinCambios, resultado);
        _notificacionServicioMock.Verify(n => n.NotificarNuevoSeguidorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _logroServicioMock.Verify(l => l.VerificarYDarLogro(It.IsAny<int>(), It.IsAny<LogroEnum>()), Times.Never);
    }

    [Fact]
    public async Task QueAlAlcanzar5SeguidosSeOtorgueLogroConAmigos()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10, 20);
        ConfigurarNeo4jEscritura(relacionesCreadas: 1);
        ConfigurarNeo4jContadores(totalSeguidos: 5, totalSeguidores: 1);

        //Act
        var resultado = await _servicio.SeguirUsuario(10, 20);

        //Assert
        Assert.Equal(ResultadoSeguimiento.Aplicado, resultado);
        _logroServicioMock.Verify(l => l.VerificarYDarLogro(10, LogroEnum.ConAmigos), Times.Once);
        _logroServicioMock.Verify(l => l.VerificarYDarLogro(20, LogroEnum.Influencer), Times.Never);
    }

    [Fact]
    public async Task QueAlAlcanzar50SeguidoresSeOtorgueLogroInfluencer()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10, 20);
        ConfigurarNeo4jEscritura(relacionesCreadas: 1);
        ConfigurarNeo4jContadores(totalSeguidos: 2, totalSeguidores: 50);

        //Act
        var resultado = await _servicio.SeguirUsuario(10, 20);

        //Assert
        Assert.Equal(ResultadoSeguimiento.Aplicado, resultado);
        _logroServicioMock.Verify(l => l.VerificarYDarLogro(20, LogroEnum.Influencer), Times.Once);
        _logroServicioMock.Verify(l => l.VerificarYDarLogro(10, LogroEnum.ConAmigos), Times.Never);
    }

    #endregion

    #region Dejar de Seguir

    [Fact]
    public async Task QueDejarDeSeguirseASiMismoDevuelvaAutoSeguimientoSinConsultarUsuarios()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10);

        //Act
        var resultado = await _servicio.DejarDeSeguirUsuario(10, 10);

        //Assert
        Assert.Equal(ResultadoSeguimiento.AutoSeguimiento, resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(10, 20)]
    [InlineData(20, 10)]
    public async Task QueSiFaltaCualquieraDeLosUsuariosDevuelvaUsuarioNoEncontradoAlDejarDeSeguir(
        int idUsuarioExistente,
        int idUsuarioInexistente)
    {
        //Arrange
        ConfigurarUsuariosExistentes(idUsuarioExistente);

        //Act
        var resultado = await _servicio.DejarDeSeguirUsuario(10, 20);

        //Assert
        Assert.Equal(ResultadoSeguimiento.UsuarioNoEncontrado, resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(idUsuarioExistente, It.IsAny<CancellationToken>()), Times.Once);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(idUsuarioInexistente, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueAlEliminarRelacionExitosamenteDevuelvaAplicado()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10, 20);
        ConfigurarNeo4jEscritura(relacionesEliminadas: 1);

        //Act
        var resultado = await _servicio.DejarDeSeguirUsuario(10, 20);

        //Assert
        Assert.Equal(ResultadoSeguimiento.Aplicado, resultado);
    }

    [Fact]
    public async Task QueSiNoHabiaRelacionDevuelvaSinCambios()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10, 20);
        ConfigurarNeo4jEscritura(relacionesEliminadas: 0);

        //Act
        var resultado = await _servicio.DejarDeSeguirUsuario(10, 20);

        //Assert
        Assert.Equal(ResultadoSeguimiento.SinCambios, resultado);
    }

    #endregion

    #region Listado de Seguidos y Seguidores

    [Fact]
    public async Task QueListarSeguidosDevuelvaNullSiUsuarioNoExisteSinAccederANeo4j()
    {
        //Arrange
        ConfigurarUsuariosExistentes();

        //Act
        var resultado = await _servicio.ListarSeguidos(99);

        //Assert
        Assert.Null(resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(99, It.IsAny<CancellationToken>()), Times.Once);
        _driverMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueListarSeguidoresDevuelvaNullSiUsuarioNoExisteSinAccederANeo4j()
    {
        //Arrange
        ConfigurarUsuariosExistentes();

        //Act
        var resultado = await _servicio.ListarSeguidores(99);

        //Assert
        Assert.Null(resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(99, It.IsAny<CancellationToken>()), Times.Once);
        _driverMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueListarSeguidosDevuelvaAutoresSiUsuarioExiste()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10);
        ConfigurarNeo4jLecturaIds("idUsuario", 20, 30);
        _usuarioRepositorioMock
            .Setup(r => r.ObtenerUsuariosPorIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AutorResumenDto>
            {
                new() { Id = 20, Username = "usuario20" },
                new() { Id = 30, Username = "usuario30" }
            });

        //Act
        var resultado = await _servicio.ListarSeguidos(10);

        //Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        Assert.Equal(new[] { 20, 30 }, resultado.Select(u => u.Id));
    }

    [Fact]
    public async Task QueListarSeguidoresDevuelvaAutoresSiUsuarioExiste()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10);
        ConfigurarNeo4jLecturaIds("idUsuario", 40, 50);
        _usuarioRepositorioMock
            .Setup(r => r.ObtenerUsuariosPorIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AutorResumenDto>
            {
                new() { Id = 40, Username = "seguidor40" },
                new() { Id = 50, Username = "seguidor50" }
            });

        //Act
        var resultado = await _servicio.ListarSeguidores(10);

        //Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        Assert.Equal(new[] { 40, 50 }, resultado.Select(u => u.Id));
    }

    #endregion

    #region Obtención de IDs de Seguidos

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task QueObtenerSeguidosIdsFalleSiUsuarioIdEsInvalido(int usuarioIdInvalido)
    {
        //Arrange & Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _servicio.ObtenerSeguidosIdsAsync(usuarioIdInvalido));
    }

    [Fact]
    public async Task QueObtenerSeguidosIdsFalleSiUsuarioNoExiste()
    {
        //Arrange
        ConfigurarUsuariosExistentes();

        //Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _servicio.ObtenerSeguidosIdsAsync(99));
    }

    [Fact]
    public async Task QueObtenerSeguidosIdsDevuelvaColeccionDeIdsDeNeo4j()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10);
        ConfigurarNeo4jLecturaIds("id", 20, 30);

        //Act
        var resultado = await _servicio.ObtenerSeguidosIdsAsync(10);

        //Assert
        Assert.Equal(new[] { 20, 30 }, resultado);
    }

    #endregion

    #region Helpers Privados

    private void ConfigurarUsuariosExistentes(params int[] idsExistentes)
    {
        var set = idsExistentes.ToHashSet();
        _usuarioRepositorioMock
            .Setup(r => r.ExisteUsuario(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => set.Contains(id));
    }

    private void ConfigurarNeo4jEscritura(int relacionesCreadas = 0, int relacionesEliminadas = 0)
    {
        var countersMock = new Mock<ICounters>();
        countersMock.Setup(c => c.RelationshipsCreated).Returns(relacionesCreadas);
        countersMock.Setup(c => c.RelationshipsDeleted).Returns(relacionesEliminadas);

        var summaryMock = new Mock<IResultSummary>();
        summaryMock.Setup(s => s.Counters).Returns(countersMock.Object);

        var cursor = TestDispatchProxy<IResultCursor>.Create((metodo, _) =>
        {
            if (metodo.Name == nameof(IResultCursor.ConsumeAsync))
                return Task.FromResult(summaryMock.Object);
            if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                return ValueTask.CompletedTask;
            throw new NotSupportedException(metodo.Name);
        });

        _runnerMock
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(cursor);
    }

    private void ConfigurarNeo4jContadores(int totalSeguidos, int totalSeguidores)
    {
        _runnerMock
            .Setup(r => r.RunAsync(It.Is<string>(q => q.Contains("ContarSeguidosQuery") || q.Contains("-[:SIGUE_A]->(:Usuario)")), It.IsAny<object>()))
            .ReturnsAsync(CrearCursorContador(totalSeguidos));

        _runnerMock
            .Setup(r => r.RunAsync(It.Is<string>(q => q.Contains("ContarSeguidoresQuery") || q.Contains("(:Usuario)-[:SIGUE_A]->")), It.IsAny<object>()))
            .ReturnsAsync(CrearCursorContador(totalSeguidores));
    }

    private static IResultCursor CrearCursorContador(int total)
    {
        var registro = CrearRecord(new Dictionary<string, object> { ["total"] = total });
        int fetched = 0;

        return TestDispatchProxy<IResultCursor>.Create((metodo, _) =>
        {
            if (metodo.Name == nameof(IResultCursor.FetchAsync))
                return Task.FromResult(Interlocked.Increment(ref fetched) == 1);
            if (metodo.Name == "get_Current")
                return registro;
            if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                return ValueTask.CompletedTask;
            throw new NotSupportedException(metodo.Name);
        });
    }

    private void ConfigurarNeo4jLecturaIds(string campoId, params int[] ids)
    {
        var registros = ids.Select(id => CrearRecord(new Dictionary<string, object> { [campoId] = id })).ToList();
        int fetchIndex = -1;

        var cursor = TestDispatchProxy<IResultCursor>.Create((metodo, _) =>
        {
            if (metodo.Name == nameof(IResultCursor.FetchAsync))
            {
                fetchIndex++;
                return Task.FromResult(fetchIndex < registros.Count);
            }
            if (metodo.Name == "get_Current")
                return registros[fetchIndex];
            if (metodo.Name == nameof(IAsyncEnumerable<IRecord>.GetAsyncEnumerator))
                return new RecordEnumerator(registros);
            if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                return ValueTask.CompletedTask;
            throw new NotSupportedException(metodo.Name);
        });

        _runnerMock
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(cursor);
    }

    private static IRecord CrearRecord(Dictionary<string, object> valores)
    {
        return TestDispatchProxy<IRecord>.Create((metodo, args) =>
        {
            if (metodo.Name == "get_Item")
            {
                var clave = (string)args![0]!;
                return valores.TryGetValue(clave, out var val) ? val : throw new KeyNotFoundException(clave);
            }
            throw new NotSupportedException(metodo.Name);
        });
    }

    private sealed class RecordEnumerator(IReadOnlyList<IRecord> registros) : IAsyncEnumerator<IRecord>
    {
        private int _indice = -1;
        public IRecord Current => registros[_indice];
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(++_indice < registros.Count);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    #endregion
}
