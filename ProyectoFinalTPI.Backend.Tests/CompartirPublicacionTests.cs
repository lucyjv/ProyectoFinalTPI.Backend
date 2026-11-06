using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CompartirPublicacionTests
{
    #region Repost de Publicaciones

    [Fact]
    public async Task QueElEndpointRepostearDevuelvaOk()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await factory.Seed();

        //Act
        using var response = await client.PostAsync("/api/publicaciones/1/repost?usuarioId=2", null);

        //Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadFromJsonAsync<RepostPublicacionDto>();
        Assert.NotNull(dto);
        Assert.Equal(1, dto.PublicacionId);
        Assert.True(dto.DioRepost);
    }

    [Fact]
    public async Task QueElEndpointQuitarRepostDevuelvaOk()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await factory.Seed();

        //Act
        using var response = await client.DeleteAsync("/api/publicaciones/1/repost?usuarioId=2");

        //Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadFromJsonAsync<RepostPublicacionDto>();
        Assert.NotNull(dto);
        Assert.Equal(1, dto.PublicacionId);
        Assert.False(dto.DioRepost);
    }

    #endregion

    #region Citar y Cascada de Publicaciones

    [Fact]
    public async Task QueElEndpointCitarCreeNuevaPublicacion()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await factory.Seed();

        var requestPayload = new
        {
            AutorId = 2,
            Descripcion = "Comentario de cita"
        };

        //Act
        using var response = await client.PostAsJsonAsync("/api/publicaciones/1/citar", requestPayload);

        //Assert
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<Resultado>();
        Assert.NotNull(body);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cita = await db.Publicaciones.SingleAsync(x => x.Id == body.id);

        Assert.Equal(1, cita.PublicacionOriginalId);
        Assert.Equal("Comentario de cita", cita.Descripcion);
        Assert.Equal(2, cita.UsuarioId);
    }

    [Fact]
    public async Task QueEliminarPublicacionOriginalElimineCompartidasEnCascada()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await factory.Seed();

        db.Publicaciones.Add(new Publicacion
        {
            Id = 2,
            Titulo = "Recuerdo Original",
            Descripcion = "Compartiendo post",
            Fecha = DateTime.UtcNow,
            Categoria = CategoriaEnum.Lugares,
            LugarId = 1,
            UsuarioId = 2,
            PublicacionOriginalId = 1, 
            UrlMultimedia = "https://example.com/foto.jpg"
        });
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Publicaciones.CountAsync());

        //Act
        var original = await db.Publicaciones.FindAsync(1);
        db.Publicaciones.Remove(original!);
        await db.SaveChangesAsync();

        //Assert
        Assert.Empty(await db.Publicaciones.ToListAsync());
    }

    #endregion

    #region Clases e Infraestructura Auxiliar

    private record Resultado(int id);

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public Mock<IMultimediaServicio> MediaMock { get; } = new();

        public ApiFactory()
        {
            MediaMock
                .Setup(m => m.SubirAsync(It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<MultimediaEnum>()))
                .ReturnsAsync("https://media.example/test");
        }

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
                services.AddSingleton<IMultimediaServicio>(MediaMock.Object);
                services.RemoveAll<IDriver>();
                services.AddSingleton<IDriver>(CrearNeo4jDriverFake());
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

        private static IDriver CrearNeo4jDriverFake()
        {
            var dioRepost = false;

            var transaccion = TestDispatchProxy<IAsyncQueryRunner>.Create((metodo, argumentos) =>
            {
                if (metodo.Name != nameof(IAsyncQueryRunner.RunAsync))
                    throw new NotSupportedException(metodo.Name);

                var query = (string)argumentos![0]!;
                var registros = new List<IRecord>();

                if (query.Contains("MERGE (usuario)-[r:REPOSTEO]->(publicacion)"))
                {
                    dioRepost = true;
                }
                else if (query.Contains("DELETE r"))
                {
                    dioRepost = false;
                    var rec = TestDispatchProxy<IRecord>.Create((m, args) =>
                    {
                        if (m.Name != "get_Item") throw new NotSupportedException(m.Name);
                        return ((string)args![0]!) switch
                        {
                            "cantidad" => 1,
                            _ => throw new KeyNotFoundException()
                        };
                    });
                    registros.Add(rec);
                }
                else if (query.Contains("r IS NOT NULL AS dioRepost"))
                {
                    var rec = TestDispatchProxy<IRecord>.Create((m, args) =>
                    {
                        if (m.Name != "get_Item") throw new NotSupportedException(m.Name);
                        return ((string)args![0]!) switch
                        {
                            "cantidadReposts" => dioRepost ? 1 : 0,
                            "dioRepost" => dioRepost,
                            _ => throw new KeyNotFoundException()
                        };
                    });
                    registros.Add(rec);
                }
                else if (query.Contains("dioRepost"))
                {
                    var rec = TestDispatchProxy<IRecord>.Create((m, args) =>
                    {
                        if (m.Name != "get_Item") throw new NotSupportedException(m.Name);
                        return ((string)args![0]!) switch
                        {
                            "cantidadReposts" => dioRepost ? 1 : 0,
                            "dioRepost" => dioRepost,
                            _ => throw new KeyNotFoundException()
                        };
                    });
                    registros.Add(rec);
                }

                var cursor = TestDispatchProxy<IResultCursor>.Create((m, _) =>
                {
                    if (m.Name == nameof(IAsyncEnumerable<IRecord>.GetAsyncEnumerator))
                        return new RecordEnumerator(registros);
                    if (m.Name == nameof(IAsyncDisposable.DisposeAsync))
                        return ValueTask.CompletedTask;

                    throw new NotSupportedException($"No se esperaba llamar a {m.Name}.");
                });

                return Task.FromResult(cursor);
            });

            var session = TestDispatchProxy<IAsyncSession>.Create((metodo, argumentos) =>
            {
                if (metodo.Name is nameof(IAsyncSession.ExecuteReadAsync) or nameof(IAsyncSession.ExecuteWriteAsync))
                    return ((Delegate)argumentos![0]!).DynamicInvoke(transaccion);
                if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                    return ValueTask.CompletedTask;

                throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}.");
            });

            return TestDispatchProxy<IDriver>.Create((metodo, _) =>
            {
                if (metodo.Name == nameof(IDriver.AsyncSession))
                    return session;
                if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                    return ValueTask.CompletedTask;

                throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}.");
            });
        }

        private sealed class RecordEnumerator(IReadOnlyList<IRecord> registros) : IAsyncEnumerator<IRecord>
        {
            private int _indice = -1;
            public IRecord Current => registros[_indice];
            public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(++_indice < registros.Count);
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    #endregion
}
