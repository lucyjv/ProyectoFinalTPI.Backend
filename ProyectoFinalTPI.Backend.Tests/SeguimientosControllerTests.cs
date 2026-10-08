using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class SeguimientosControllerTests
{
    [Theory]
    [InlineData(ResultadoSeguimiento.Aplicado, HttpStatusCode.Created, "Ahora seguís a este usuario.")]
    [InlineData(ResultadoSeguimiento.SinCambios, HttpStatusCode.OK, "Ya seguías a este usuario.")]
    [InlineData(ResultadoSeguimiento.AutoSeguimiento, HttpStatusCode.BadRequest, "No podés seguirte a vos mismo.")]
    [InlineData(ResultadoSeguimiento.UsuarioNoEncontrado, HttpStatusCode.NotFound, "No se encontró uno o ambos usuarios.")]
    public async Task SeguirUsuario_devuelve_el_codigo_y_mensaje_segun_el_resultado(
        ResultadoSeguimiento resultado,
        HttpStatusCode codigoEsperado,
        string mensajeEsperado)
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ResultadoSeguir = resultado;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/seguimientos",
            new { idUsuario = 10, idUsuarioASeguir = 20 });

        Assert.Equal(codigoEsperado, response.StatusCode);
        Assert.Contains(mensajeEsperado, await response.Content.ReadAsStringAsync());
        Assert.Equal((10, 20), factory.Seguimiento.UltimosIds);
    }

    [Fact]
    public async Task Falla_inesperada_al_seguir_devuelve_500_sin_exponer_detalles()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ErrorSeguir = new InvalidOperationException("detalle interno");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/seguimientos",
            new { idUsuario = 10, idUsuarioASeguir = 20 });
        var contenido = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Ocurrió un error al procesar el seguimiento.", contenido);
        Assert.DoesNotContain("detalle interno", contenido);
    }

    [Fact]
    public async Task Request_con_id_invalido_devuelve_400_sin_invocar_el_servicio()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/seguimientos",
            new { idUsuario = 0, idUsuarioASeguir = 20 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(factory.Seguimiento.FueInvocado);
    }

    [Theory]
    [InlineData(ResultadoSeguimiento.Aplicado, HttpStatusCode.NoContent)]
    [InlineData(ResultadoSeguimiento.SinCambios, HttpStatusCode.NoContent)]
    [InlineData(ResultadoSeguimiento.AutoSeguimiento, HttpStatusCode.BadRequest)]
    [InlineData(ResultadoSeguimiento.UsuarioNoEncontrado, HttpStatusCode.NotFound)]
    public async Task DejarDeSeguir_devuelve_el_codigo_segun_el_resultado(
        ResultadoSeguimiento resultado,
        HttpStatusCode codigoEsperado)
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ResultadoDejarDeSeguir = resultado;
        using var client = factory.CreateClient();
        using var request = CrearRequestDelete(10, 20);

        using var response = await client.SendAsync(request);

        Assert.Equal(codigoEsperado, response.StatusCode);
        Assert.Equal((10, 20), factory.Seguimiento.UltimosIds);
        if (codigoEsperado == HttpStatusCode.NoContent)
        {
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Falla_inesperada_al_dejar_de_seguir_devuelve_500_sin_exponer_detalles()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ErrorDejarDeSeguir = new InvalidOperationException("detalle interno");
        using var client = factory.CreateClient();
        using var request = CrearRequestDelete(10, 20);

        using var response = await client.SendAsync(request);
        var contenido = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Ocurrió un error al procesar el seguimiento.", contenido);
        Assert.DoesNotContain("detalle interno", contenido);
    }

    [Fact]
    public async Task Delete_con_id_invalido_devuelve_400_sin_invocar_el_servicio()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = CrearRequestDelete(0, 20);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(factory.Seguimiento.FueInvocado);
    }

    [Fact]
    public async Task ListarSeguidos_devuelve_los_usuarios_que_sigue()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ResultadoSeguidos = new List<AutorResumenDto>
        {
            new() { Id = 20, Username = "usuario_20" },
            new() { Id = 30, Username = "usuario_30" }
        };
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seguimientos/10/seguidos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((10, 0), factory.Seguimiento.UltimosIds);
        var seguidos = await response.Content.ReadFromJsonAsync<List<AutorResumenDto>>();
        Assert.NotNull(seguidos);
        Assert.Collection(
            seguidos,
            usuario =>
            {
                Assert.Equal(20, usuario.Id);
                Assert.Equal("usuario_20", usuario.Username);
            },
            usuario =>
            {
                Assert.Equal(30, usuario.Id);
                Assert.Equal("usuario_30", usuario.Username);
            });
    }

    [Fact]
    public async Task ListarSeguidos_devuelve_lista_vacia_si_no_sigue_a_usuarios()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ResultadoSeguidos = Array.Empty<AutorResumenDto>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seguimientos/10/seguidos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadFromJsonAsync<List<AutorResumenDto>>() ?? new());
    }

    [Fact]
    public async Task ListarSeguidos_devuelve_404_si_el_usuario_no_existe()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ResultadoSeguidos = null;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seguimientos/999/seguidos");
        var contenido = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No se encontro el usuario", contenido);
    }

    [Fact]
    public async Task ListarSeguidos_con_id_invalido_devuelve_400_sin_invocar_el_servicio()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seguimientos/0/seguidos");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(factory.Seguimiento.FueInvocado);
    }

    [Fact]
    public async Task Falla_inesperada_al_listar_seguidos_devuelve_500_sin_exponer_detalles()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ErrorListarSeguidos = new InvalidOperationException("detalle interno");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seguimientos/10/seguidos");
        var contenido = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Ocurrio un error al listar los usuarios seguidos", contenido);
        Assert.DoesNotContain("detalle interno", contenido);
    }

    [Fact]
    public async Task ListarSeguidores_devuelve_los_usuarios_que_siguen_al_usuario()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ResultadoSeguidores = new List<AutorResumenDto>
        {
            new() { Id = 20, Username = "usuario_20" },
            new() { Id = 30, Username = "usuario_30" }
        };
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seguimientos/10/seguidores");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((10, 0), factory.Seguimiento.UltimosIds);
        var seguidores = await response.Content.ReadFromJsonAsync<List<AutorResumenDto>>();
        Assert.NotNull(seguidores);
        Assert.Collection(
            seguidores,
            usuario =>
            {
                Assert.Equal(20, usuario.Id);
                Assert.Equal("usuario_20", usuario.Username);
            },
            usuario =>
            {
                Assert.Equal(30, usuario.Id);
                Assert.Equal("usuario_30", usuario.Username);
            });
    }

    [Fact]
    public async Task ListarSeguidores_devuelve_lista_vacia_si_nadie_sigue_al_usuario()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ResultadoSeguidores = Array.Empty<AutorResumenDto>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seguimientos/10/seguidores");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadFromJsonAsync<List<AutorResumenDto>>() ?? new());
    }

    [Fact]
    public async Task ListarSeguidores_devuelve_404_si_el_usuario_no_existe()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ResultadoSeguidores = null;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seguimientos/999/seguidores");
        var contenido = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No se encontro el usuario", contenido);
    }

    [Fact]
    public async Task ListarSeguidores_con_id_invalido_devuelve_400_sin_invocar_el_servicio()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seguimientos/0/seguidores");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(factory.Seguimiento.FueInvocado);
    }

    [Fact]
    public async Task Falla_inesperada_al_listar_seguidores_devuelve_500_sin_exponer_detalles()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.ErrorListarSeguidores = new InvalidOperationException("detalle interno");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seguimientos/10/seguidores");
        var contenido = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Ocurrio un error al listar los seguidores", contenido);
        Assert.DoesNotContain("detalle interno", contenido);
    }

    private static HttpRequestMessage CrearRequestDelete(int idUsuario, int idUsuarioASeguir)
    {
        return new HttpRequestMessage(HttpMethod.Delete, "/api/seguimientos")
        {
            Content = JsonContent.Create(new { idUsuario, idUsuarioASeguir })
        };
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public SeguimientoFake Seguimiento { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

                services.RemoveAll<ISeguimientoServicio>();
                services.AddSingleton<ISeguimientoServicio>(Seguimiento);
            });
        }
    }

    private sealed class SeguimientoFake : ISeguimientoServicio
    {
        public ResultadoSeguimiento ResultadoSeguir { get; set; }
        public ResultadoSeguimiento ResultadoDejarDeSeguir { get; set; }
        public IReadOnlyList<ProyectoFinalTPI.Backend.Dtos.Usuario.AutorResumenDto>? ResultadoSeguidos { get; set; }
        public Exception? ErrorListarSeguidos { get; set; }
        public IReadOnlyList<ProyectoFinalTPI.Backend.Dtos.Usuario.AutorResumenDto>? ResultadoSeguidores { get; set; }
        public Exception? ErrorListarSeguidores { get; set; }
        public Exception? ErrorSeguir { get; set; }
        public Exception? ErrorDejarDeSeguir { get; set; }
        public (int Seguidor, int Seguido) UltimosIds { get; private set; }
        public bool FueInvocado { get; private set; }

        public Task<IReadOnlyList<ProyectoFinalTPI.Backend.Dtos.Usuario.AutorResumenDto>?> ListarSeguidos(
            int idUsuario, CancellationToken cancellationToken = default)
        {
            FueInvocado = true;
            UltimosIds = (idUsuario, 0);
            return ErrorListarSeguidos is null
                ? Task.FromResult(ResultadoSeguidos)
                : Task.FromException<IReadOnlyList<ProyectoFinalTPI.Backend.Dtos.Usuario.AutorResumenDto>?>(ErrorListarSeguidos);
        }

        public Task<IReadOnlyList<ProyectoFinalTPI.Backend.Dtos.Usuario.AutorResumenDto>?> ListarSeguidores(
            int idUsuario, CancellationToken cancellationToken = default)
        {
            FueInvocado = true;
            UltimosIds = (idUsuario, 0);
            return ErrorListarSeguidores is null
                ? Task.FromResult(ResultadoSeguidores)
                : Task.FromException<IReadOnlyList<ProyectoFinalTPI.Backend.Dtos.Usuario.AutorResumenDto>?>(ErrorListarSeguidores);
        }

        public Task<ResultadoSeguimiento> SeguirUsuario(
            int idUsuario,
            int idUsuarioASeguir,
            CancellationToken cancellationToken = default)
        {
            FueInvocado = true;
            UltimosIds = (idUsuario, idUsuarioASeguir);

            return ErrorSeguir is null
                ? Task.FromResult(ResultadoSeguir)
                : Task.FromException<ResultadoSeguimiento>(ErrorSeguir);
        }

        public Task<ResultadoSeguimiento> DejarDeSeguirUsuario(
            int idUsuario,
            int idUsuarioASeguir,
            CancellationToken cancellationToken = default)
        {
            FueInvocado = true;
            UltimosIds = (idUsuario, idUsuarioASeguir);

            return ErrorDejarDeSeguir is null
                ? Task.FromResult(ResultadoDejarDeSeguir)
                : Task.FromException<ResultadoSeguimiento>(ErrorDejarDeSeguir);
        }
    }
}
