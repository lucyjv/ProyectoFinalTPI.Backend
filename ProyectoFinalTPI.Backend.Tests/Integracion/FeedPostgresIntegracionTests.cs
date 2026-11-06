using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class FeedPostgresIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    [Fact]
    public async Task QueLasCandidatasSeanVisiblesLivianasYPaginadasEnPostgresReal()
    {
        //Arrange: conexión exclusivamente del contenedor; no se leen User Secrets.
        var conexion = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = "feed_test_" + Guid.NewGuid().ToString("N"), Pooling = false
        }.ConnectionString;
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(conexion, o => o.UseNetTopologySuite()).Options;
        await using var db = new ApplicationDbContext(opciones);
        try
        {
            await db.Database.MigrateAsync();
            var autor = new Usuario_Personal
            {
                Username = "feed", Email = "feed@example.com", Contraseña = "hash",
                Nombre = "Prueba", Apellido = "Feed", FechaNacimiento = new DateOnly(2000, 1, 1)
            };
            var lugar = new Lugar { Nombre = "Buenos Aires", Direccion = "Buenos Aires", Coordenadas = new Point(-58.4, -34.6) { SRID = 4326 } };
            var fecha = DateTime.UtcNow.AddDays(-1);
            var publicaciones = Enumerable.Range(1, 4).Select(i => new Publicacion
            {
                Titulo = "Recuerdo " + i, Descripcion = "Contenido", Usuario = autor, Lugar = lugar,
                Fecha = fecha, FechaCreación = fecha, Categoria = CategoriaEnum.Cine, EstaOculto = i == 4
            }).ToArray();
            db.Publicaciones.AddRange(publicaciones);
            await db.SaveChangesAsync();
            var repositorio = new PublicacionRepositorio(db);

            //Act
            var primera = await repositorio.ObtenerCandidatasGeneralesFeed(DateTime.UtcNow, null, 2);
            var ultima = primera[^1];
            var segunda = await repositorio.ObtenerCandidatasGeneralesFeed(DateTime.UtcNow,
                new() { FechaCreacion = ultima.FechaCreacion, Id = ultima.Id }, 2);
            var ids = publicaciones.Select(p => p.Id).ToArray();
            var caracteristicas = await repositorio.ObtenerCaracteristicasFeed(ids);
            var visibles = await repositorio.ObtenerIdsVisibles(ids);

            //Assert
            Assert.Equal(2, primera.Count);
            Assert.Single(segunda);
            Assert.Empty(primera.Select(p => p.Id).Intersect(segunda.Select(p => p.Id)));
            Assert.Equal(3, caracteristicas.Count);
            Assert.Equal(3, visibles.Count);
            Assert.DoesNotContain(publicaciones[3].Id, visibles);
            Assert.All(caracteristicas, p =>
            {
                Assert.Equal(-34.6, p.Latitud, 5);
                Assert.Equal(-58.4, p.Longitud, 5);
                Assert.Equal(CategoriaEnum.Cine, p.Categoria);
            });
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
