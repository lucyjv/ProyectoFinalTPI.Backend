using Microsoft.EntityFrameworkCore;
using Moq;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class PublicacionRepositorioNeo4jFeedTests
{
    private readonly Mock<IDriver> _driverMock = new();
    private readonly Mock<IAsyncSession> _sessionMock = new();
    private readonly Mock<IAsyncQueryRunner> _runnerMock = new();
    private string? _queryEjecutada;
    private object? _parametrosEjecutados;

    public PublicacionRepositorioNeo4jFeedTests()
    {
        _driverMock.Setup(d => d.AsyncSession()).Returns(_sessionMock.Object);

        _sessionMock
            .Setup(s => s.ExecuteWriteAsync(It.IsAny<Func<IAsyncQueryRunner, Task>>(), It.IsAny<Action<TransactionConfigBuilder>>()))
            .Returns<Func<IAsyncQueryRunner, Task>, Action<TransactionConfigBuilder>>((work, _) => work(_runnerMock.Object));

        _sessionMock
            .Setup(s => s.ExecuteReadAsync(It.IsAny<Func<IAsyncQueryRunner, Task<IReadOnlyList<RecomendacionPublicacionDto>>>>(), It.IsAny<Action<TransactionConfigBuilder>>()))
            .Returns<Func<IAsyncQueryRunner, Task<IReadOnlyList<RecomendacionPublicacionDto>>>, Action<TransactionConfigBuilder>>((work, _) => work(_runnerMock.Object));

        _sessionMock
            .Setup(s => s.ExecuteReadAsync(It.IsAny<Func<IAsyncQueryRunner, Task<IReadOnlyList<InteraccionFeedDto>>>>(), It.IsAny<Action<TransactionConfigBuilder>>()))
            .Returns<Func<IAsyncQueryRunner, Task<IReadOnlyList<InteraccionFeedDto>>>, Action<TransactionConfigBuilder>>((work, _) => work(_runnerMock.Object));
    }

    #region Registro de Impresiones en Feed

    [Fact]
    public async Task QueRegistrarImpresionesUseMergeYEnvieIdsSinRepetir()
    {
        //Arrange
        ConfigurarCursor();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, _driverMock.Object);

        //Act
        await repositorio.RegistrarImpresionesFeed(10, new[] { 2, 2, 3 });

        //Assert
        Assert.NotNull(_queryEjecutada);
        Assert.Contains("MERGE (usuario)-[vista:VIO]->(publicacion)", _queryEjecutada);
        Assert.Contains("SET vista.ultimaVez", _queryEjecutada);
        Assert.Equal(10, Parametro<int>(_parametrosEjecutados, "usuarioId"));
        Assert.Equal(new[] { 2, 3 }, Parametro<int[]>(_parametrosEjecutados, "publicacionesIds"));
        Assert.InRange(Parametro<long>(_parametrosEjecutados, "ultimaVezUnixMs"),
            DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds(),
            DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task QueRegistrarImpresionesConListaVaciaNoConsulteNeo4j()
    {
        //Arrange
        ConfigurarCursor();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, _driverMock.Object);

        //Act
        await repositorio.RegistrarImpresionesFeed(10, Array.Empty<int>());

        //Assert
        Assert.Null(_queryEjecutada);
    }

    #endregion

    #region Recomendaciones del Feed

    [Fact]
    public async Task QueRecomendacionesConsiderenLikesPuntuacionesEtiquetasYVistasRecientes()
    {
        //Arrange
        ConfigurarCursor();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, _driverMock.Object);
        const long vistasDesde = 123456789;

        //Act
        var resultado = await repositorio.ObtenerRecomendacionesFeed(
            10, 50, vistasDesde, new PesosRecomendacionFeedDto(4, 5, 2));

        //Assert
        Assert.Empty(resultado);
        Assert.NotNull(_queryEjecutada);
        Assert.Contains("LE_GUSTA", _queryEjecutada);
        Assert.Contains("PUNTUO", _queryEjecutada);
        Assert.Contains("ETIQUETADO_EN", _queryEjecutada);
        Assert.Contains("VIO", _queryEjecutada);
        Assert.Contains("WITH DISTINCT usuario, semilla", _queryEjecutada);
        Assert.Contains("UNION ALL", _queryEjecutada);
        Assert.Contains("$pesoLike", _queryEjecutada);
        Assert.Contains("$pesoPuntuacion", _queryEjecutada);
        Assert.Contains("$pesoEtiqueta", _queryEjecutada);
        Assert.Equal(vistasDesde, Parametro<long>(_parametrosEjecutados, "vistasDesdeUnixMs"));
        Assert.Equal(50, Parametro<int>(_parametrosEjecutados, "cantidadMaxima"));
        Assert.Equal(4, Parametro<int>(_parametrosEjecutados, "pesoLike"));
        Assert.Equal(5, Parametro<int>(_parametrosEjecutados, "pesoPuntuacion"));
        Assert.Equal(2, Parametro<int>(_parametrosEjecutados, "pesoEtiqueta"));
    }

    #endregion

    #region Interacciones del Feed

    [Fact]
    public async Task QueInteraccionesConsultenLikesPuntuacionesYVistas()
    {
        //Arrange
        ConfigurarCursor();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, _driverMock.Object);

        //Act
        var resultado = await repositorio.ObtenerInteraccionesFeed(10);

        //Assert
        Assert.Empty(resultado);
        Assert.NotNull(_queryEjecutada);
        Assert.Contains("LE_GUSTA", _queryEjecutada);
        Assert.Contains("PUNTUO", _queryEjecutada);
        Assert.Contains("VIO", _queryEjecutada);
        Assert.Equal(10, Parametro<int>(_parametrosEjecutados, "usuarioId"));
    }

    [Fact]
    public async Task QueInteraccionesConservenPuntuacionYFechaDeVista()
    {
        //Arrange
        var registro = TestDispatchProxy<IRecord>.Create((metodo, argumentos) =>
        {
            if (metodo.Name != "get_Item") throw new NotSupportedException(metodo.Name);
            return ((string)argumentos![0]!) switch
            {
                "publicacionId" => 42L,
                "dioLike" => true,
                "miPuntuacion" => 4L,
                "ultimaImpresionUnixMs" => 123456789L,
                _ => throw new KeyNotFoundException()
            };
        });

        ConfigurarCursor(new[] { registro });
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, _driverMock.Object);

        //Act
        var interaccion = Assert.Single(await repositorio.ObtenerInteraccionesFeed(10));

        //Assert
        Assert.Equal(42, interaccion.PublicacionId);
        Assert.True(interaccion.DioLike);
        Assert.Equal(4, interaccion.MiPuntuacion);
        Assert.Equal(123456789L, interaccion.UltimaImpresionUnixMs);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private void ConfigurarCursor(IReadOnlyList<IRecord>? registros = null)
    {
        registros ??= Array.Empty<IRecord>();
        var cursor = TestDispatchProxy<IResultCursor>.Create((metodo, _) =>
        {
            if (metodo.Name == nameof(IAsyncEnumerable<IRecord>.GetAsyncEnumerator))
                return new RecordEnumerator(registros);
            if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                return ValueTask.CompletedTask;
            throw new NotSupportedException(metodo.Name);
        });

        _runnerMock
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((q, p) =>
            {
                _queryEjecutada = q;
                _parametrosEjecutados = p;
            })
            .ReturnsAsync(cursor);
    }

    private static ApplicationDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(opciones);
    }

    private static T Parametro<T>(object? parametros, string nombre)
        => (T)(parametros?.GetType().GetProperty(nombre)?.GetValue(parametros)
            ?? throw new InvalidOperationException($"Falta el parámetro {nombre}."));

    private sealed class RecordEnumerator(IReadOnlyList<IRecord> registros) : IAsyncEnumerator<IRecord>
    {
        private int _indice = -1;
        public IRecord Current => registros[_indice];
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(++_indice < registros.Count);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    #endregion
}
