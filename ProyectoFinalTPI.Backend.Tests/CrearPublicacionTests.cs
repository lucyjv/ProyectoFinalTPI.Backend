using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CrearPublicacionTests
{
    #region Creación Exitosa y Persistencia

    [Theory]
    [InlineData(false, "0")]
    [InlineData(false, "1")]
    [InlineData(true, "0")]
    [InlineData(true, "1")]
    public async Task QueCrearPersistaPublicacionYLugar(bool archivo, string tipo)
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await factory.Seed();
        using var form = Form(archivo: archivo, overrides: new() { ["TipoMultimedia"] = tipo });

        //Act
        using var response = await client.PostAsync("/api/publicaciones", form);

        //Assert
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<Resultado>();
        Assert.NotNull(body);
        Assert.Equal($"/api/publicaciones/{body.id}", response.Headers.Location?.OriginalString);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var p = await db.Publicaciones.Include(p => p.Lugar).SingleAsync();

        Assert.Equal(body.id, p.Id);
        Assert.Equal("Recuerdo", p.Titulo);
        Assert.Equal("Descripción", p.Descripcion);
        Assert.Equal(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), p.Fecha);
        Assert.Equal(CategoriaEnum.Lugares, p.Categoria);
        Assert.Equal(1, p.UsuarioId);
        Assert.Equal("https://media.example/test", p.UrlMultimedia);
        Assert.Equal((MultimediaEnum)int.Parse(tipo), p.TipoMultimedia);
        Assert.False(p.EstaOculto);
        Assert.Null(p.MotivoOculto);
        Assert.Equal(DateTimeKind.Utc, p.FechaCreación.Kind);
        Assert.InRange(p.FechaCreación, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
        Assert.Equal("Buenos Aires", p.Lugar.Nombre);
        Assert.Equal(-58.3816, p.Lugar.Coordenadas.X);
        Assert.Equal(-34.6037, p.Lugar.Coordenadas.Y);
        Assert.Equal(4326, p.Lugar.Coordenadas.SRID);
        Assert.Equal(1, await db.Lugares.CountAsync());
        Assert.Equal(1, factory.MediaCalls);
        Assert.Equal(p.TipoMultimedia, factory.MediaTipo);
        Assert.Equal(archivo ? "foto.jpg" : null, factory.MediaNombre);
        Assert.Equal(archivo ? null : "https://example.com/foto.jpg", factory.MediaUrl);
        Assert.Equal(archivo ? new byte[] { 1, 2, 3 } : null, factory.MediaBytes);
    }

    #endregion

    #region Validaciones de Entrada

    [Theory]
    [InlineData("Titulo", "")]
    [InlineData("Descripcion", "")]
    [InlineData("NombreLugar", "")]
    [InlineData("Fecha", "")]
    [InlineData("Fecha", "no-es-fecha")]
    [InlineData("Categoria", "999")]
    [InlineData("TipoMultimedia", "999")]
    [InlineData("Latitud", "91")]
    [InlineData("Longitud", "-181")]
    [InlineData("AutorId", "0")]
    [InlineData("UrlArchivo", "http://example.com/foto.jpg")]
    [InlineData("UrlArchivo", "invalid")]
    public async Task QueCrearInvalidoDevuelva400SinSubirNiPersistir(string campo, string valor)
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await factory.Seed();
        using var form = Form(overrides: new() { [campo] = valor });

        //Act
        using var response = await client.PostAsync("/api/publicaciones", form);

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors", await response.Content.ReadAsStringAsync());
        await AssertSinEfectos(factory);
    }

    [Fact]
    public async Task QueCrearConArchivoYUrlDevuelva400()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var form = Form(archivo: true, overrides: new() { ["UrlArchivo"] = "https://example.com/a.jpg" });

        //Act
        using var response = await client.PostAsync("/api/publicaciones", form);

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertSinEfectos(factory);
    }

    [Fact]
    public async Task QueCrearConAutorInexistenteDevuelva404SinSubir()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var form = Form();

        //Act
        using var response = await client.PostAsync("/api/publicaciones", form);

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("El usuario indicado no existe.", await response.Content.ReadAsStringAsync());
        await AssertSinEfectos(factory);
    }

    [Fact]
    public async Task QueFallaDeMultimediaNoPersistaPublicacion()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await factory.Seed();
        factory.MediaFalla = true;
        using var form = Form();

        //Act
        using var response = await client.PostAsync("/api/publicaciones", form);

        //Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertSinEfectos(factory, 1);
    }

    #endregion

    #region Opciones de Multimedia

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, "  ")]
    [InlineData(true, null)]
    public async Task QueMultimediaOpcionalPersistaTextoSinInvocarProveedor(bool requerida, string? url)
    {
        //Arrange
        await using var factory = new ApiFactory { RequerirMultimedia = requerida };
        using var client = factory.CreateClient();
        await factory.Seed();
        factory.MediaFalla = true;
        using var form = Form(sinMultimedia: true);
        if (url != null) form.Add(new StringContent(url), "UrlArchivo");

        //Act
        using var response = await client.PostAsync("/api/publicaciones", form);

        //Assert
        if (requerida)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Debe subir un archivo o indicar una URL", await response.Content.ReadAsStringAsync());
            await AssertSinEfectos(factory);
            return;
        }

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publicacion = await db.Publicaciones.SingleAsync();

        Assert.Equal(string.Empty, publicacion.UrlMultimedia);
        Assert.Equal("Recuerdo", publicacion.Titulo);
        Assert.Equal(0, factory.MediaCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueRequerirMultimediaSigaAceptandoArchivosOEnlacesValidos(bool archivo)
    {
        //Arrange
        await using var factory = new ApiFactory { RequerirMultimedia = true };
        using var client = factory.CreateClient();
        await factory.Seed();
        using var form = Form(archivo: archivo);

        //Act
        var response = await client.PostAsync("/api/publicaciones", form);

        //Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, factory.MediaCalls);
    }

    [Fact]
    public async Task QueArchivoVacioSeaInvalidoConMultimediaOpcional()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await factory.Seed();
        using var form = Form(sinMultimedia: true);
        form.Add(new ByteArrayContent(Array.Empty<byte>()), "Archivo", "vacio.jpg");

        //Act
        var response = await client.PostAsync("/api/publicaciones", form);

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertSinEfectos(factory);
    }

    #endregion

    #region Infraestructura y Helpers de Test

    private static async Task AssertSinEfectos(ApiFactory factory, int llamadas = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Publicaciones.ToListAsync());
        Assert.Empty(await db.Lugares.ToListAsync());
        Assert.Equal(llamadas, factory.MediaCalls);
    }

    private static MultipartFormDataContent Form(bool archivo = false, Dictionary<string, string>? overrides = null, bool sinMultimedia = false)
    {
        var campos = new Dictionary<string, string>
        {
            ["Titulo"] = "Recuerdo",
            ["Descripcion"] = "Descripción",
            ["Fecha"] = "2000-01-01T00:00:00Z",
            ["Categoria"] = "0",
            ["NombreLugar"] = "Buenos Aires",
            ["Latitud"] = "-34.6037",
            ["Longitud"] = "-58.3816",
            ["TipoMultimedia"] = "0",
            ["AutorId"] = "1"
        };

        if (!archivo && !sinMultimedia) campos["UrlArchivo"] = "https://example.com/foto.jpg";
        if (overrides != null)
        {
            foreach (var campo in overrides) campos[campo.Key] = campo.Value;
        }

        var form = new MultipartFormDataContent();
        foreach (var campo in campos) form.Add(new StringContent(campo.Value), campo.Key);
        if (archivo) form.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "Archivo", "foto.jpg");
        return form;
    }

    private record Resultado(int id);

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public Mock<IMultimediaServicio> MediaMock { get; } = new();
        public bool RequerirMultimedia { get; init; }

        public int MediaCalls { get; private set; }
        public bool MediaFalla { get; set; }
        public MultimediaEnum? MediaTipo { get; private set; }
        public string? MediaNombre { get; private set; }
        public string? MediaUrl { get; private set; }
        public byte[]? MediaBytes { get; private set; }

        public ApiFactory()
        {
            MediaMock
                .Setup(m => m.SubirAsync(It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<MultimediaEnum>()))
                .Returns(async (Stream? archivo, string? nombreArchivo, string? urlExterna, MultimediaEnum tipo) =>
                {
                    MediaCalls++;
                    if (MediaFalla) throw new InvalidOperationException("Error simulado de multimedia");
                    MediaTipo = tipo;
                    MediaNombre = nombreArchivo;
                    MediaUrl = urlExterna;
                    if (archivo != null)
                    {
                        using var copia = new MemoryStream();
                        await archivo.CopyToAsync(copia);
                        MediaBytes = copia.ToArray();
                    }
                    return "https://media.example/test";
                });
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
                services.PostConfigure<OpcionesPublicacion>(opciones => opciones.RequerirMultimedia = RequerirMultimedia);
            });
        }

        public async Task Seed()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Usuarios.Add(new Usuario_Personal { Id = 1, Username = "test", Email = "test@example.com" });
            await db.SaveChangesAsync();
        }
    }

    #endregion
}
