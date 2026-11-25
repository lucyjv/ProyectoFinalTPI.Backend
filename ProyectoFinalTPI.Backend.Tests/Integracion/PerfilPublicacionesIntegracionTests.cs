using System.Net.Http.Json;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Point = NetTopologySuite.Geometries.Point;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class PerfilPublicacionesIntegracionTests(PostgisFixture postgres, Neo4jLogrosFixture neo4j)
    : IClassFixture<PostgisFixture>, IClassFixture<Neo4jLogrosFixture>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueAplicarMigracionDePuntuacionesPermitaCargarPerfilYDetalle(bool puntuacionesMigradas)
    {
        await using var factory = new PublicacionesApiFactory(postgres, neo4j.Driver);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var usuario = new Usuario_Personal { Id = 10, Username = "perfil", Email = "perfil@example.com" };
        var lugar = new Lugar
        {
            Id = 1, Nombre = "Obelisco", Direccion = "Buenos Aires",
            Coordenadas = new Point(-58.38, -34.60) { SRID = 4326 }
        };
        var original = new Publicacion
        {
            Id = 1, Titulo = "Original", Descripcion = "Recuerdo original", Usuario = usuario, Lugar = lugar,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            FechaCreación = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            Adjuntos = [new AdjuntoPublicacion { Url = "https://example.com/recuerdo.jpg", Orden = 0 }]
        };
        context.Publicaciones.Add(original);
        context.Publicaciones.Add(new Publicacion
        {
            Id = 2, Titulo = "Cita", Descripcion = "Mi cita", Usuario = usuario, Lugar = lugar,
            Fecha = original.Fecha, FechaCreación = original.FechaCreación.AddDays(1),
            PublicacionOriginal = original
        });
        await context.SaveChangesAsync();

        await using (var sesion = neo4j.Driver.AsyncSession())
        {
            await (await sesion.RunAsync("""
                MERGE (usuario:Usuario {postgresId: 20})
                MERGE (publicacion:Publicacion {postgresId: 1})
                MERGE (usuario)-[puntuacion:PUNTUO]->(publicacion)
                SET puntuacion.puntaje = 4
                """)).ConsumeAsync();
        }

        if (!puntuacionesMigradas)
        {
            // Solo modifica la base desechable creada por esta factory.
            const string migracionPuntuaciones = "20261120165404_AgregarPuntuacionesPublicacionConCupo";
            var anterior = context.Database.GetMigrations()
                .TakeWhile(m => m != migracionPuntuaciones).Last();
            await context.GetService<IMigrator>().MigrateAsync(anterior);

            using var sinMigracion = await client.GetAsync($"/api/publicaciones/usuario/{usuario.Id}");
            Assert.Equal(HttpStatusCode.InternalServerError, sinMigracion.StatusCode);
            using var detalleSinMigracion = await client.GetAsync("/api/publicaciones/1");
            Assert.Equal(HttpStatusCode.InternalServerError, detalleSinMigracion.StatusCode);

            var sql = await File.ReadAllTextAsync(Path.Combine(
                AppContext.BaseDirectory, "restaurar-publicaciones-perfil.sql"));
            await context.Database.ExecuteSqlRawAsync(sql);
            await context.Database.ExecuteSqlRawAsync(sql);
            Assert.Contains(migracionPuntuaciones, await context.Database.GetAppliedMigrationsAsync());
        }

        using var response = await client.GetAsync($"/api/publicaciones/usuario/{usuario.Id}");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var publicaciones = await response.Content.ReadFromJsonAsync<List<PublicacionDetalleDto>>();
        Assert.NotNull(publicaciones);
        Assert.Equal(new[] { 2, 1 }, publicaciones.Select(p => p.Id));
        Assert.Single(publicaciones[1].Adjuntos);
        Assert.Equal(4, publicaciones[1].PuntosTotales);
        Assert.Equal(original.Id, publicaciones[0].PublicacionOriginal!.Id);

        var detalle = await client.GetFromJsonAsync<PublicacionDetalleDto>("/api/publicaciones/1");
        Assert.NotNull(detalle);
        Assert.Equal(4, detalle.PuntosTotales);
    }
}
