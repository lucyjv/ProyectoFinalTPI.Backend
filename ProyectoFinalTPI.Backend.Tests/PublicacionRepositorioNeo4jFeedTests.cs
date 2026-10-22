using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class PublicacionRepositorioNeo4jFeedTests
{
    [Fact]
    public async Task RegistrarImpresionesUsaMergeYEnviaIdsSinRepetir()
    {
        var neo4j = new Neo4jFeedFake();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, neo4j.Driver);

        await repositorio.RegistrarImpresionesFeed(10, new[] { 2, 2, 3 });

        Assert.Contains("MERGE (usuario)-[vista:VIO]->(publicacion)", neo4j.Query);
        Assert.Contains("SET vista.ultimaVez", neo4j.Query);
        Assert.Equal(10, Parametro<int>(neo4j.Parametros, "usuarioId"));
        Assert.Equal(new[] { 2, 3 }, Parametro<int[]>(neo4j.Parametros, "publicacionesIds"));
        Assert.InRange(Parametro<long>(neo4j.Parametros, "ultimaVezUnixMs"),
            DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds(),
            DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task RegistrarImpresionesConListaVaciaNoConsultaNeo4j()
    {
        var neo4j = new Neo4jFeedFake();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, neo4j.Driver);

        await repositorio.RegistrarImpresionesFeed(10, Array.Empty<int>());

        Assert.Null(neo4j.Query);
    }

    [Fact]
    public async Task RecomendacionesConsideranLikesPuntuacionesEtiquetasYVistasRecientes()
    {
        var neo4j = new Neo4jFeedFake();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, neo4j.Driver);
        const long vistasDesde = 123456789;

        var resultado = await repositorio.ObtenerRecomendacionesFeed(10, 50, vistasDesde);

        Assert.Empty(resultado);
        Assert.Contains("LE_GUSTA", neo4j.Query);
        Assert.Contains("PUNTUO", neo4j.Query);
        Assert.Contains("ETIQUETADO_EN", neo4j.Query);
        Assert.Contains("VIO", neo4j.Query);
        Assert.Equal(vistasDesde, Parametro<long>(neo4j.Parametros, "vistasDesdeUnixMs"));
        Assert.Equal(50, Parametro<int>(neo4j.Parametros, "cantidadMaxima"));
    }

    [Fact]
    public async Task InteraccionesConsultanLikesPuntuacionesYVistas()
    {
        var neo4j = new Neo4jFeedFake();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, neo4j.Driver);

        var resultado = await repositorio.ObtenerInteraccionesFeed(10);

        Assert.Empty(resultado);
        Assert.Contains("LE_GUSTA", neo4j.Query);
        Assert.Contains("PUNTUO", neo4j.Query);
        Assert.Contains("VIO", neo4j.Query);
        Assert.Equal(10, Parametro<int>(neo4j.Parametros, "usuarioId"));
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

    private sealed class Neo4jFeedFake
    {
        public string? Query { get; private set; }
        public object? Parametros { get; private set; }
        public IDriver Driver { get; }

        public Neo4jFeedFake()
        {
            var cursor = TestDispatchProxy<IResultCursor>.Create((metodo, _) =>
            {
                if (metodo.Name == nameof(IAsyncEnumerable<IRecord>.GetAsyncEnumerator))
                    return new EmptyRecordEnumerator();
                if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                    return ValueTask.CompletedTask;
                throw new NotSupportedException(metodo.Name);
            });
            var transaccion = TestDispatchProxy<IAsyncQueryRunner>.Create((metodo, argumentos) =>
            {
                if (metodo.Name != nameof(IAsyncQueryRunner.RunAsync))
                    throw new NotSupportedException(metodo.Name);
                Query = (string)argumentos![0]!;
                Parametros = argumentos[1];
                return Task.FromResult(cursor);
            });
            var sesion = TestDispatchProxy<IAsyncSession>.Create((metodo, argumentos) =>
            {
                if (metodo.Name is nameof(IAsyncSession.ExecuteReadAsync) or nameof(IAsyncSession.ExecuteWriteAsync))
                    return ((Delegate)argumentos![0]!).DynamicInvoke(transaccion);
                if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                    return ValueTask.CompletedTask;
                throw new NotSupportedException(metodo.Name);
            });
            Driver = TestDispatchProxy<IDriver>.Create((metodo, _) =>
                metodo.Name == nameof(IDriver.AsyncSession)
                    ? sesion
                    : throw new NotSupportedException(metodo.Name));
        }
    }

    private sealed class EmptyRecordEnumerator : IAsyncEnumerator<IRecord>
    {
        public IRecord Current => null!;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(false);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
