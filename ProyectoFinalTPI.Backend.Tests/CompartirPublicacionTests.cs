using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests
{
    public class CompartirPublicacionTests
    {
        [Fact]
        public async Task Compartir_crea_nueva_publicacion_y_apunta_al_original()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            await factory.Seed();

            var requestPayload = new
            {
                PublicacionOriginalId = 1,
                AutorId = 2,
                Descripcion = "Este lugar es increible!"
            };

            using var response = await client.PostAsJsonAsync("/api/publicaciones/compartir", requestPayload);

            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

            var body = await response.Content.ReadFromJsonAsync<Resultado>();
            Assert.NotNull(body);
            Assert.Equal($"/api/publicaciones/{body.id}", response.Headers.Location?.OriginalString);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var p = await db.Publicaciones.Include(x => x.Lugar).SingleAsync(x => x.Id == body.id);

            Assert.Equal(2, p.UsuarioId);
            Assert.Equal(1, p.PublicacionOriginalId);
            Assert.Equal("Este lugar es increible!", p.Descripcion);
            Assert.Equal("Recuerdo Original", p.Titulo); // Copiado
            Assert.Equal(CategoriaEnum.Lugares, p.Categoria); // Copiado
            Assert.Equal("https://example.com/foto.jpg", p.UrlMultimedia); // Copiado
            Assert.False(p.EstaOculto);
        }

        [Fact]
        public async Task Eliminar_publicacion_original_elimina_compartidas_en_cascada()
        {
            await using var factory = new ApiFactory();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await factory.Seed();

            // Insertamos la publicacion compartida
            db.Publicaciones.Add(new Publicacion
            {
                Id = 2,
                Titulo = "Recuerdo Original",
                Descripcion = "Compartiendo post",
                Fecha = DateTime.UtcNow,
                Categoria = CategoriaEnum.Lugares,
                LugarId = 1,
                UsuarioId = 2,
                PublicacionOriginalId = 1, // Apunta a la original
                UrlMultimedia = "https://example.com/foto.jpg"
            });
            await db.SaveChangesAsync();

            Assert.Equal(2, await db.Publicaciones.CountAsync());

            // Eliminamos la original
            var original = await db.Publicaciones.FindAsync(1);
            db.Publicaciones.Remove(original!);
            await db.SaveChangesAsync();

            // La cascada debería haber eliminado la compartida también (In-Memory provider hace track cascade)
            Assert.Empty(await db.Publicaciones.ToListAsync());
        }

        private record Resultado(int id);

        private sealed class ApiFactory : WebApplicationFactory<Program>
        {
            public MediaFake Media { get; } = new();

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                    var nombre = Guid.NewGuid().ToString();
                    services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(nombre));
                    services.RemoveAll<IMultimediaServicio>();
                    services.AddSingleton<IMultimediaServicio>(Media);
                });
            }

            public async Task Seed()
            {
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                db.Usuarios.Add(new Usuario_Personal { Id = 1, Username = "test1", Email = "test1@example.com" });
                db.Usuarios.Add(new Usuario_Personal { Id = 2, Username = "test2", Email = "test2@example.com" });

                db.Lugares.Add(new Lugar { Id = 1, Nombre = "Lugar 1", Coordenadas = new NetTopologySuite.Geometries.Point(0, 0) });

                db.Publicaciones.Add(new Publicacion
                {
                    Id = 1,
                    Titulo = "Recuerdo Original",
                    Descripcion = "Original",
                    Fecha = DateTime.UtcNow,
                    Categoria = CategoriaEnum.Lugares,
                    LugarId = 1,
                    UsuarioId = 1,
                    UrlMultimedia = "https://example.com/foto.jpg"
                });

                await db.SaveChangesAsync();
            }
        }

        private sealed class MediaFake : IMultimediaServicio
        {
            public Task<string> SubirAsync(Stream? archivo, string? nombreArchivo, string? urlExterna, MultimediaEnum tipo)
            {
                return Task.FromResult("https://media.example/test");
            }
        }
    }
}
