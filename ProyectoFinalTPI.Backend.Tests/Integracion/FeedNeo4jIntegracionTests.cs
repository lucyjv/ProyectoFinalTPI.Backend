using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

// Fixture propia de esta clase: solo escribe en un Neo4j desechable de Testcontainers.
[Trait("Categoria", "Integracion")]
public class FeedNeo4jIntegracionTests(Neo4jLogrosFixture neo4j) : IClassFixture<Neo4jLogrosFixture>
{
    private async Task Preparar(string query)
    {
        await using var sesion = neo4j.Driver.AsyncSession();
        await (await sesion.RunAsync("MATCH (n) DETACH DELETE n")).ConsumeAsync();
        await (await sesion.RunAsync(query)).ConsumeAsync();
    }

    private static ApplicationDbContext CrearContexto() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task QueLasSemillasRespetenAntiguedadTopeYUnaPublicacionPorSemilla()
    {
        //Arrange
        await Preparar("""
            CREATE (u:Usuario {postgresId: 1}), (a:Publicacion {postgresId: 10}),
                   (b:Publicacion {postgresId: 11}), (c:Publicacion {postgresId: 12})
            CREATE (u)-[:LE_GUSTA {creadaEn: datetime()}]->(a),
                   (u)-[:PUNTUO {puntaje: 5, fecha: datetime()}]->(a),
                   (u)-[:LE_GUSTA {creadaEn: datetime() - duration('P365D')}]->(b),
                   (u)-[:PUNTUO {puntaje: 4, fecha: datetime()}]->(c)
            """);
        await using var db = CrearContexto();
        var repositorio = new PublicacionRepositorio(db, neo4j.Driver);
        //Act
        var semillas = await repositorio.ObtenerSemillasFeed(1, DateTimeOffset.UtcNow.AddDays(-180).ToUnixTimeMilliseconds(), 100);
        //Assert
        Assert.Equal(new[] { 10, 12 }, semillas.Select(s => s.PublicacionId).Order().ToArray());
        Assert.True(Assert.Single(semillas, s => s.PublicacionId == 10).DioLike);
        Assert.Single(await repositorio.ObtenerSemillasFeed(1, 0, 1));
    }

    [Fact]
    public async Task QueUnaPuntuacionRecienteConserveElLikeAntiguoDeLaMismaSemilla()
    {
        //Arrange
        await Preparar("""
            CREATE (u:Usuario {postgresId: 1}), (p:Publicacion {postgresId: 10}),
                   (baja:Publicacion {postgresId: 11})
            CREATE (u)-[:LE_GUSTA {creadaEn: datetime() - duration('P365D')}]->(p),
                   (u)-[:PUNTUO {puntaje: 5, fecha: datetime()}]->(p),
                   (u)-[:PUNTUO {puntaje: 1, fecha: datetime()}]->(baja)
            """);
        await using var db = CrearContexto();
        var repositorio = new PublicacionRepositorio(db, neo4j.Driver);

        //Act
        var semillas = await repositorio.ObtenerSemillasFeed(1,
            DateTimeOffset.UtcNow.AddDays(-180).ToUnixTimeMilliseconds(), 100);

        //Assert
        var semilla = Assert.Single(semillas);
        Assert.Equal(10, semilla.PublicacionId);
        Assert.True(semilla.DioLike);
        Assert.Equal(5, semilla.MiPuntuacion);
    }

    [Fact]
    public async Task QueLasCandidatasDetectenInteraccionesAntiguasYPuntuacionesBajas()
    {
        //Arrange
        await Preparar("""
            CREATE (u:Usuario {postgresId: 1}), (a:Publicacion {postgresId: 10}), (b:Publicacion {postgresId: 11})
            CREATE (u)-[:LE_GUSTA {creadaEn: datetime() - duration('P365D')}]->(a),
                   (u)-[:PUNTUO {puntaje: 1, fecha: datetime() - duration('P365D')}]->(b),
                   (u)-[:VIO {ultimaVez: 12345}]->(b)
            """);
        await using var db = CrearContexto();
        var repositorio = new PublicacionRepositorio(db, neo4j.Driver);
        //Act
        var estados = await repositorio.ObtenerInteraccionesCandidatasFeed(1, new[] { 10, 11, 99 });
        //Assert
        Assert.Equal(3, estados.Count);
        Assert.True(Assert.Single(estados, e => e.PublicacionId == 10).DioLike);
        var puntuado = Assert.Single(estados, e => e.PublicacionId == 11);
        Assert.Equal(1, puntuado.MiPuntuacion);
        Assert.Equal(12345L, puntuado.UltimaImpresionUnixMs);
        Assert.False(Assert.Single(estados, e => e.PublicacionId == 99).DioLike);
    }

    [Fact]
    public async Task QueElResumenNoMultipliqueLikesPuntosNiReposts()
    {
        //Arrange
        await Preparar("""
            CREATE (u:Usuario {postgresId: 1}), (v:Usuario {postgresId: 2}), (p:Publicacion {postgresId: 10})
            CREATE (u)-[:LE_GUSTA]->(p), (v)-[:LE_GUSTA]->(p),
                   (u)-[:PUNTUO {puntaje: 4}]->(p), (v)-[:PUNTUO {puntaje: 5}]->(p),
                   (u)-[:REPOSTEO]->(p), (v)-[:REPOSTEO]->(p), (u)-[:GUARDO]->(p)
            """);
        await using var db = CrearContexto();
        //Act
        var resumen = await new PublicacionRepositorio(db, neo4j.Driver).ObtenerResumenInteraccionesFeed(new[] { 10, 99 }, 1);
        //Assert
        Assert.Equal(2, resumen[10].CantidadLikes);
        Assert.Equal(9, resumen[10].PuntosTotales);
        Assert.Equal(4, resumen[10].MiPuntuacion);
        Assert.Equal(2, resumen[10].CantidadReposts);
        Assert.True(resumen[10].DioLike && resumen[10].DioRepost && resumen[10].Guardada);
        Assert.Equal(0, resumen[99].CantidadLikes);
        Assert.False(resumen[99].Guardada);
    }

    [Fact]
    public async Task QueLasRecomendacionesUsenLasSemillasYExcluyanLikesPuntuacionesYVistasRecientes()
    {
        //Arrange
        await Preparar("""
            CREATE (u:Usuario {postgresId: 1}), (v:Usuario {postgresId: 2}),
                (s:Publicacion {postgresId: 10}), (a:Publicacion {postgresId: 11}),
                (b:Publicacion {postgresId: 12}), (c:Publicacion {postgresId: 13}), (d:Publicacion {postgresId: 14})
            CREATE (u)-[:LE_GUSTA]->(s), (v)-[:LE_GUSTA]->(s),
                (v)-[:LE_GUSTA]->(a), (v)-[:LE_GUSTA]->(b), (v)-[:LE_GUSTA]->(c), (v)-[:LE_GUSTA]->(d),
                (u)-[:LE_GUSTA]->(b), (u)-[:PUNTUO {puntaje: 1}]->(c), (u)-[:VIO {ultimaVez: timestamp()}]->(d)
            """);
        await using var db = CrearContexto();
        var repositorio = new PublicacionRepositorio(db, neo4j.Driver);
        //Act
        var candidatas = await repositorio.ObtenerRecomendacionesFeed(1, 500,
            DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeMilliseconds(), new(2, 3, 1), new[] { 10 }, new(20, 25));
        //Assert
        Assert.Equal(11, Assert.Single(candidatas).PublicacionId);
        Assert.Equal(2, candidatas[0].PuntajeColaborativo);
        Assert.Empty(await repositorio.ObtenerRecomendacionesFeed(1, 500, 0, new(2, 3, 1), Array.Empty<int>(), new(20, 25)));
    }

    [Fact]
    public async Task QueLosRepostsPaginadosDesempatenPorUsuarioYRespetenFraccionesDeMilisegundo()
    {
        //Arrange
        await Preparar("""
            CREATE (u:Usuario {postgresId: 1}), (v:Usuario {postgresId: 2}), (p:Publicacion {postgresId: 10})
            CREATE (u)-[:REPOSTEO {creadaEnUnixMs: 1000}]->(p), (v)-[:REPOSTEO {creadaEnUnixMs: 1000}]->(p)
            """);
        await using var db = CrearContexto();
        var repositorio = new PublicacionRepositorio(db, neo4j.Driver);
        //Act
        var primera = await repositorio.ObtenerRepostsDeSeguidosPaginados(new[] { 1, 2 }, null, 1);
        //Assert
        Assert.Equal(new[] { 2, 1 }, primera.Select(r => r.ReposteadorId));
        var siguiente = await repositorio.ObtenerRepostsDeSeguidosPaginados(new[] { 1, 2 },
            new() { FechaCreacion = primera[0].Fecha, Id = 10, ReposteadorId = 2 }, 1);
        Assert.Equal(1, Assert.Single(siguiente).ReposteadorId);
        var antesDelOriginal = await repositorio.ObtenerRepostsDeSeguidosPaginados(new[] { 1, 2 },
            new() { FechaCreacion = primera[0].Fecha.AddTicks(10), Id = 1 }, 2);
        Assert.Equal(2, antesDelOriginal.Count);
    }
}
