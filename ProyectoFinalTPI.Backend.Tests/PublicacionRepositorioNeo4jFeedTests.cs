using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
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
    public async Task CrearGuardadoUsaRelacionGuardoYParametrosCorrectos()
    {
        var neo4j = new Neo4jFeedFake();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, neo4j.Driver);

        await repositorio.CrearGuardado(42, 10);

        Assert.Contains("MERGE (usuario)-[g:GUARDO]->(publicacion)", neo4j.Query);
        Assert.Contains("SET g.creadaEn", neo4j.Query);
        Assert.Equal(10, Parametro<int>(neo4j.Parametros, "usuarioId"));
        Assert.Equal(42, Parametro<int>(neo4j.Parametros, "publicacionId"));
    }

    [Fact]
    public async Task ObtenerResumenGuardadosConListaVaciaNoConsultaNeo4j()
    {
        var neo4j = new Neo4jFeedFake();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, neo4j.Driver);

        var resultado = await repositorio.ObtenerResumenGuardados(Array.Empty<int>(), 10);

        Assert.Empty(resultado);
        Assert.Null(neo4j.Query);
    }

    [Fact]
    public async Task ObtenerIdsPublicacionesGuardadasConsultaRelacionGuardo()
    {
        var neo4j = new Neo4jFeedFake();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, neo4j.Driver);

        var resultado = await repositorio.ObtenerIdsPublicacionesGuardadas(10);

        Assert.Contains("MATCH (usuario:Usuario {postgresId: $usuarioId})-[g:GUARDO]->(p:Publicacion)", neo4j.Query);
        Assert.Contains("ORDER BY coalesce(g.creadaEnUnixMs, 0) DESC", neo4j.Query);
        Assert.Equal(10, Parametro<int>(neo4j.Parametros, "usuarioId"));
    }

    [Fact]
    public async Task RecomendacionesConsideranLikesPuntuacionesEtiquetasYVistasRecientes()
    {
        var neo4j = new Neo4jFeedFake();
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, neo4j.Driver);
        const long vistasDesde = 123456789;

        var resultado = await repositorio.ObtenerRecomendacionesFeed(
            10, 50, vistasDesde, new PesosRecomendacionFeedDto(4, 5, 2));

        Assert.Empty(resultado);
        Assert.Contains("LE_GUSTA", neo4j.Query);
        Assert.Contains("PUNTUO", neo4j.Query);
        Assert.Contains("ETIQUETADO_EN", neo4j.Query);
        Assert.Contains("VIO", neo4j.Query);
        Assert.Contains("WITH DISTINCT usuario, semilla", neo4j.Query);
        Assert.Contains("UNION ALL", neo4j.Query);
        Assert.Contains("$pesoLike", neo4j.Query);
        Assert.Contains("$pesoPuntuacion", neo4j.Query);
        Assert.Contains("$pesoEtiqueta", neo4j.Query);
        Assert.Equal(vistasDesde, Parametro<long>(neo4j.Parametros, "vistasDesdeUnixMs"));
        Assert.Equal(50, Parametro<int>(neo4j.Parametros, "cantidadMaxima"));
        Assert.Equal(4, Parametro<int>(neo4j.Parametros, "pesoLike"));
        Assert.Equal(5, Parametro<int>(neo4j.Parametros, "pesoPuntuacion"));
        Assert.Equal(2, Parametro<int>(neo4j.Parametros, "pesoEtiqueta"));
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

    [Fact]
    public async Task InteraccionesConservanPuntuacionYFechaDeVista()
    {
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
        var neo4j = new Neo4jFeedFake(new[] { registro });
        await using var contexto = CrearContexto();
        var repositorio = new PublicacionRepositorio(contexto, neo4j.Driver);

        var interaccion = Assert.Single(await repositorio.ObtenerInteraccionesFeed(10));

        Assert.Equal(42, interaccion.PublicacionId);
        Assert.True(interaccion.DioLike);
        Assert.Equal(4, interaccion.MiPuntuacion);
        Assert.Equal(123456789L, interaccion.UltimaImpresionUnixMs);
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

        public Neo4jFeedFake(IReadOnlyList<IRecord>? registros = null)
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

    private sealed class RecordEnumerator(IReadOnlyList<IRecord> registros) : IAsyncEnumerator<IRecord>
    {
        private int _indice = -1;
        public IRecord Current => registros[_indice];
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(++_indice < registros.Count);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
