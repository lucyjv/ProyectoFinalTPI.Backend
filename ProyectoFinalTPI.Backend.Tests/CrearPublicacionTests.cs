using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CrearPublicacionTests
{
    [Theory]
    [InlineData(false, "0")]
    [InlineData(false, "1")]
    [InlineData(true, "0")]
    [InlineData(true, "1")]
    public async Task Crear_persiste_publicacion_y_lugar(bool archivo, string tipo)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await factory.Seed();
        using var form = Form(archivo: archivo, overrides: new() { ["TipoMultimedia"] = tipo });
        using var response = await client.PostAsync("/api/publicaciones", form);
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
        Assert.Equal(1, factory.Media.Calls);
        Assert.Equal(p.TipoMultimedia, factory.Media.Tipo);
        Assert.Equal(archivo ? "foto.jpg" : null, factory.Media.Nombre);
        Assert.Equal(archivo ? null : "https://example.com/foto.jpg", factory.Media.Url);
        Assert.Equal(archivo ? new byte[] { 1, 2, 3 } : null, factory.Media.Bytes);
    }

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
    [InlineData("UrlArchivo", "")]
    public async Task Invalido_devuelve_400_sin_subir_ni_persistir(string campo, string valor)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await factory.Seed();
        using var form = Form(overrides: new() { [campo] = valor });
        using var response = await client.PostAsync("/api/publicaciones", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors", await response.Content.ReadAsStringAsync());
        await AssertSinEfectos(factory);
    }

    [Fact]
    public async Task Archivo_y_url_devuelve_400()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var form = Form(archivo: true, overrides: new() { ["UrlArchivo"] = "https://example.com/a.jpg" });
        using var response = await client.PostAsync("/api/publicaciones", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertSinEfectos(factory);
    }

    [Fact]
    public async Task Autor_inexistente_devuelve_404_sin_subir()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var form = Form();
        using var response = await client.PostAsync("/api/publicaciones", form);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("El usuario indicado no existe.", await response.Content.ReadAsStringAsync());
        await AssertSinEfectos(factory);
    }

    [Fact]
    public async Task Falla_multimedia_no_persiste()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await factory.Seed();
        factory.Media.Falla = true;
        using var form = Form();
        using var response = await client.PostAsync("/api/publicaciones", form);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertSinEfectos(factory, 1);
    }

    private static async Task AssertSinEfectos(ApiFactory factory, int llamadas = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Publicaciones.ToListAsync());
        Assert.Empty(await db.Lugares.ToListAsync());
        Assert.Equal(llamadas, factory.Media.Calls);
    }

    private static MultipartFormDataContent Form(bool archivo = false, Dictionary<string, string>? overrides = null)
    {
        var campos = new Dictionary<string, string>
        {
            ["Titulo"] = "Recuerdo", ["Descripcion"] = "Descripción", ["Fecha"] = "2000-01-01T00:00:00Z",
            ["Categoria"] = "0", ["NombreLugar"] = "Buenos Aires", ["Latitud"] = "-34.6037",
            ["Longitud"] = "-58.3816", ["TipoMultimedia"] = "0", ["AutorId"] = "1"
        };
        if (!archivo) campos["UrlArchivo"] = "https://example.com/foto.jpg";
        if (overrides != null) foreach (var campo in overrides) campos[campo.Key] = campo.Value;
        var form = new MultipartFormDataContent();
        foreach (var campo in campos) form.Add(new StringContent(campo.Value), campo.Key);
        if (archivo) form.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "Archivo", "foto.jpg");
        return form;
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
            db.Usuarios.Add(new Usuario_Personal { Id = 1, Username = "test", Email = "test@example.com" });
            await db.SaveChangesAsync();
        }
    }

    private sealed class MediaFake : IMultimediaServicio
    {
        public int Calls;
        public bool Falla;
        public MultimediaEnum Tipo;
        public string? Nombre, Url;
        public byte[]? Bytes;
        public async Task<string> SubirAsync(Stream? archivo, string? nombreArchivo, string? urlExterna, MultimediaEnum tipo)
        {
            Calls++;
            if (Falla) throw new InvalidOperationException("Error simulado de multimedia");
            Tipo = tipo; Nombre = nombreArchivo; Url = urlExterna;
            if (archivo != null)
            {
                using var copia = new MemoryStream();
                await archivo.CopyToAsync(copia);
                Bytes = copia.ToArray();
            }
            return "https://media.example/test";
        }
    }
}
