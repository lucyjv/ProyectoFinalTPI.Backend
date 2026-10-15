using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

// API, servicio, repositorio y PostgreSQL/PostGIS reales en un contenedor temporal.
[Trait("Categoria", "Integracion")]
public class PublicacionesIntegracionTests : IClassFixture<PostgisFixture>
{
    private readonly PostgisFixture _postgres;

    public PublicacionesIntegracionTests(PostgisFixture postgres)
    {
        _postgres = postgres;
    }

    private readonly Usuario_Personal usuario = new()
    {
        Id = 10, Username = "usuarioTest", Email = "test@example.com"
    };

    private readonly Lugar lugar = new()
    {
        Id = 1, Nombre = "Obelisco", Direccion = "Buenos Aires",
        Coordenadas = new Point(-58.3816, -34.6037) { SRID = 4326 }
    };

    [Fact]
    public async Task QueSeCreeUnComentarioYSePuedaConsultarPorGet()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo", Usuario = usuario, Lugar = lugar,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        await context.SaveChangesAsync();
        var desde = DateTime.UtcNow;

        //Act
        using var response = await client.PostAsJsonAsync("/api/publicaciones/1/comentarios",
            new { contenido = "  Qué lindo recuerdo  ", usuarioId = usuario.Id });
        var result = await response.Content.ReadFromJsonAsync<ComentarioDto>();
        using var consulta = await client.GetAsync("/api/publicaciones/1/comentarios");
        var pagina = await consulta.Content.ReadFromJsonAsync<ResultadoPaginadoDto<ComentarioDto>>();
        context.ChangeTracker.Clear();
        var guardado = await context.Set<Comentario>()
            .Include(c => c.Publicacion).Include(c => c.Usuario).SingleAsync();

        //Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("Qué lindo recuerdo", result.Contenido);
        Assert.Equal(usuario.Id, result.Autor.Id);
        Assert.Equal(1, guardado.Publicacion.Id);
        Assert.False(guardado.EstaOculto);
        Assert.InRange(guardado.FechaCreacion, desde, DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
        Assert.NotNull(pagina);
        Assert.Equal(result.Id, Assert.Single(pagina.Items).Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task QueNoSeCreeUnComentarioConContenidoVacio(string? contenido)
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo", Usuario = usuario, Lugar = lugar,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        await context.SaveChangesAsync();

        //Act
        using var response = await client.PostAsJsonAsync("/api/publicaciones/1/comentarios",
            new { contenido, usuarioId = usuario.Id });
        var result = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(400, result.Status);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(!string.IsNullOrWhiteSpace(result.Detail) || result.Errors.Count > 0);
        Assert.Empty(await context.Set<Comentario>().ToListAsync());
    }

    [Fact]
    public async Task QueNoSeCreeUnComentarioSiElUsuarioNoExiste()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo", Usuario = usuario, Lugar = lugar,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        await context.SaveChangesAsync();

        //Act
        using var response = await client.PostAsJsonAsync("/api/publicaciones/1/comentarios",
            new { contenido = "Comentario", usuarioId = 99 });
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(404, result.Status);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Empty(await context.Set<Comentario>().ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueNoSeCreeUnComentarioSiLaPublicacionNoEstaDisponible(bool oculta)
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo", Usuario = usuario, Lugar = lugar,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        publicacion.EstaOculto = oculta;
        await context.SaveChangesAsync();

        //Act
        using var response = await client.PostAsJsonAsync($"/api/publicaciones/{(oculta ? 1 : 99)}/comentarios",
            new { contenido = "Comentario", usuarioId = usuario.Id });
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(404, result.Status);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Empty(await context.Set<Comentario>().ToListAsync());
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task QueNoSeCreeUnComentarioConIdsInvalidos(int publicacionId, int usuarioId)
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo", Usuario = usuario, Lugar = lugar,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        await context.SaveChangesAsync();

        //Act
        using var response = await client.PostAsJsonAsync($"/api/publicaciones/{publicacionId}/comentarios",
            new { contenido = "Comentario", usuarioId });
        var result = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(400, result.Status);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(!string.IsNullOrWhiteSpace(result.Detail) || result.Errors.Count > 0);
        Assert.Empty(await context.Set<Comentario>().ToListAsync());
    }

    [Fact]
    public async Task QueSeObtenganLosPinesDePublicacionesVisiblesDentroDelArea()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);

        context.Publicaciones.Add(new Publicacion
        {
            Id = 2, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), EstaOculto = true
        });
        var fueraDelArea = new Publicacion
        {
            Id = 3, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        fueraDelArea.Lugar = new Lugar
        {
            Id = 2, Nombre = "Fuera del área", Coordenadas = new Point(-60, -36) { SRID = 4326 }
        };
        context.Publicaciones.Add(fueraDelArea);
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/mapa?MinLatitud=-35&MaxLatitud=-34&MinLongitud=-59&MaxLongitud=-58");
        var result = await response.Content.ReadFromJsonAsync<ResultadoMapaDto>();

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(1, Assert.Single(result.Pines).Id);
        Assert.Equal(-34.6037, result.Pines[0].Latitud);
        Assert.Equal(-58.3816, result.Pines[0].Longitud);
        Assert.False(result.HayMas);
    }

    [Fact]
    public async Task QueSeFiltrenLosPinesPorCategoriaYFecha()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);

        var otraCategoria = new Publicacion
        {
            Id = 2, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        otraCategoria.Categoria = CategoriaEnum.Cine;
        var anterior = new Publicacion
        {
            Id = 3, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        anterior.Fecha = new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var fechaHasta = new Publicacion
        {
            Id = 4, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        fechaHasta.Fecha = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        context.Publicaciones.AddRange(otraCategoria, anterior, fechaHasta);
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/mapa?MinLatitud=-35&MaxLatitud=-34&MinLongitud=-59&MaxLongitud=-58&Categoria=Lugares&FechaDesde=2000-01-01T00:00:00Z&FechaHasta=2001-01-01T00:00:00Z");
        var result = await response.Content.ReadFromJsonAsync<ResultadoMapaDto>();

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(1, Assert.Single(result.Pines).Id);
    }

    [Fact]
    public async Task QueElMapaDevuelvaDoscientosPinesEIndiqueQueHayMas()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);

        for (var id = 1; id <= 201; id++)
        {
            context.Publicaciones.Add(new Publicacion
            {
                Id = id, Titulo = $"Recuerdo {id}", Usuario = usuario, Lugar = lugar,
                Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/mapa?MinLatitud=-35&MaxLatitud=-34&MinLongitud=-59&MaxLongitud=-58");
        var result = await response.Content.ReadFromJsonAsync<ResultadoMapaDto>();

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(200, result.Pines.Count);
        Assert.Equal(Enumerable.Range(1, 200), result.Pines.Select(p => p.Id));
        Assert.True(result.HayMas);
    }

    [Fact]
    public async Task QueElMapaDevuelvaBadRequestSiElFiltroEsInvalido()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/mapa?MinLatitud=40&MaxLatitud=30&MinLongitud=-59&MaxLongitud=-58");
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(400, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal("/api/publicaciones/mapa", result.Instance);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task QueSeObtengaElResumenConLaDescripcionRecortada()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/1/resumen");
        var result = await response.Content.ReadFromJsonAsync<PublicacionResumenDto>();

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("Recuerdo del Obelisco", result.Titulo);
        Assert.Equal(usuario.Id, result.Autor.Id);
        Assert.Equal(new string('A', 180) + "...", result.DescripcionBreve);
    }

    [Fact]
    public async Task QueElResumenDevuelvaNotFoundSiLaPublicacionNoExiste()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/99/resumen");
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(404, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal("/api/publicaciones/99/resumen", result.Instance);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task QueElResumenDevuelvaNotFoundSiLaPublicacionEstaOculta()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        publicacion.EstaOculto = true;
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/1/resumen");
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(404, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal("/api/publicaciones/1/resumen", result.Instance);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task QueSeObtengaElDetalleConLaDescripcionCompleta()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/1");
        var result = await response.Content.ReadFromJsonAsync<PublicacionDetalleDto>();

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("Recuerdo del Obelisco", result.Titulo);
        Assert.Equal(usuario.Id, result.Autor.Id);
        Assert.Equal(new string('A', 500), result.Descripcion);
        Assert.Equal("Obelisco", result.Lugar.Nombre);
    }

    [Fact]
    public async Task QueElDetalleDevuelvaNotFoundSiLaPublicacionNoExiste()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/99");
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(404, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal("/api/publicaciones/99", result.Instance);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task QueElDetalleDevuelvaNotFoundSiLaPublicacionEstaOculta()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        publicacion.EstaOculto = true;
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/1");
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(404, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal("/api/publicaciones/1", result.Instance);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task QueSeObtenganLosComentariosVisiblesDeLaPublicacion()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);

        var otraPublicacion = new Publicacion
        {
            Id = 2, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(otraPublicacion);
        context.AddRange(
            new Comentario
        {
            Id = 1, Contenido = "Comentario 1", Usuario = usuario, Publicacion = publicacion,
            FechaCreacion = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        },
            new Comentario
        {
            Id = 2, Contenido = "Comentario 2", Usuario = usuario, Publicacion = publicacion,
            FechaCreacion = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), EstaOculto = true
        },
            new Comentario
        {
            Id = 3, Contenido = "Comentario 3", Usuario = usuario, Publicacion = otraPublicacion,
            FechaCreacion = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/1/comentarios");
        var result = await response.Content.ReadFromJsonAsync<ResultadoPaginadoDto<ComentarioDto>>();

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        var comentario = Assert.Single(result.Items);
        Assert.Equal(1, comentario.Id);
        Assert.Equal("Comentario 1", comentario.Contenido);
        Assert.Equal(usuario.Id, comentario.Autor.Id);
    }

    [Fact]
    public async Task QueSePuedanRecorrerLasPaginasDeComentariosSinRepetirNiOmitirResultados()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);

        // Las fechas empatadas obligan a usar también el ID al paginar.
        for (var id = 1; id <= 5; id++)
        {
            context.Add(new Comentario
            {
                Id = id, Contenido = $"Comentario {id}", Usuario = usuario, Publicacion = publicacion,
                FechaCreacion = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(id == 5 ? 1 : 0)
            });
        }
        await context.SaveChangesAsync();

        //Act
        using var primeraResponse = await client.GetAsync("/api/publicaciones/1/comentarios?limite=2");
        var primera = await primeraResponse.Content.ReadFromJsonAsync<ResultadoPaginadoDto<ComentarioDto>>();
        Assert.NotNull(primera);
        Assert.NotNull(primera.SiguienteCursor);
        using var segundaResponse = await client.GetAsync(
            $"/api/publicaciones/1/comentarios?limite=2&cursor={Uri.EscapeDataString(primera.SiguienteCursor)}");
        var segunda = await segundaResponse.Content.ReadFromJsonAsync<ResultadoPaginadoDto<ComentarioDto>>();
        Assert.NotNull(segunda);
        Assert.NotNull(segunda.SiguienteCursor);
        using var terceraResponse = await client.GetAsync(
            $"/api/publicaciones/1/comentarios?limite=2&cursor={Uri.EscapeDataString(segunda.SiguienteCursor)}");
        var tercera = await terceraResponse.Content.ReadFromJsonAsync<ResultadoPaginadoDto<ComentarioDto>>();

        //Assert
        Assert.Equal(HttpStatusCode.OK, primeraResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segundaResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, terceraResponse.StatusCode);
        Assert.NotNull(tercera);
        Assert.Equal(new[] { 5, 4 }, primera.Items.Select(c => c.Id));
        Assert.Equal(new[] { 3, 2 }, segunda.Items.Select(c => c.Id));
        Assert.Equal(1, Assert.Single(tercera.Items).Id);
        Assert.Null(tercera.SiguienteCursor);
    }

    [Fact]
    public async Task QueLaUltimaPaginaDeComentariosNoDevuelvaCursor()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);

        context.AddRange(new Comentario
        {
            Id = 1, Contenido = "Comentario 1", Usuario = usuario, Publicacion = publicacion,
            FechaCreacion = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        }, new Comentario
        {
            Id = 2, Contenido = "Comentario 2", Usuario = usuario, Publicacion = publicacion,
            FechaCreacion = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/1/comentarios?limite=2");
        var result = await response.Content.ReadFromJsonAsync<ResultadoPaginadoDto<ComentarioDto>>();

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(new[] { 2, 1 }, result.Items.Select(c => c.Id));
        Assert.Null(result.SiguienteCursor);
    }

    [Fact]
    public async Task QueUnaPublicacionSinComentariosDevuelvaUnaListaVacia()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/1/comentarios");
        var result = await response.Content.ReadFromJsonAsync<ResultadoPaginadoDto<ComentarioDto>>();

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Null(result.SiguienteCursor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueLosComentariosDevuelvanNotFoundSiLaPublicacionNoEstaDisponible(bool oculta)
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);

        if (oculta)
        {
            var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
            publicacion.EstaOculto = true;
            context.Publicaciones.Add(publicacion);
            context.Add(new Comentario
        {
            Id = 1, Contenido = "Comentario 1", Usuario = usuario, Publicacion = publicacion,
            FechaCreacion = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        }
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/1/comentarios");
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(404, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal("/api/publicaciones/1/comentarios", result.Instance);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task QueLosComentariosDevuelvanBadRequestSiElCursorEsInvalido()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync("/api/publicaciones/1/comentarios?cursor=cursor-invalido!");
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(400, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal("/api/publicaciones/1/comentarios", result.Instance);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task QueLosComentariosDevuelvanBadRequestSiElLimiteEsInvalido(int limite)
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Usuarios.Add(usuario);
        context.Lugares.Add(lugar);
        var publicacion = new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del Obelisco", Descripcion = new string('A', 500),
            Usuario = usuario, Lugar = lugar, Categoria = CategoriaEnum.Lugares,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Publicaciones.Add(publicacion);
        await context.SaveChangesAsync();

        //Act
        using var response = await client.GetAsync($"/api/publicaciones/1/comentarios?limite={limite}");
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(400, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal("/api/publicaciones/1/comentarios", result.Instance);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

