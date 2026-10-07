using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class SeguimientosControllerTests
{
    // xUnit lo ejecuta 4 veces, una por cada resultado
    [Theory]
    [InlineData(ResultadoSeguirUsuario.Seguido, HttpStatusCode.Created, "Ahora seguís a este usuario.")]
    [InlineData(ResultadoSeguirUsuario.YaLoSeguía, HttpStatusCode.OK, "Ya seguías a este usuario.")]
    [InlineData(ResultadoSeguirUsuario.AutoSeguimiento, HttpStatusCode.BadRequest, "No podés seguirte a vos mismo.")]
    [InlineData(ResultadoSeguirUsuario.UsuarioNoEncontrado, HttpStatusCode.NotFound, "No se encontró uno o ambos usuarios.")]
    public async Task SeguirUsuario_devuelve_el_codigo_y_mensaje_segun_el_resultado(
        ResultadoSeguirUsuario resultado,
        HttpStatusCode codigoEsperado,
        string mensajeEsperado)
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.Resultado = resultado;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/seguimientos",
            new { idUsuario = 10, idUsuarioASeguir = 20 });

        Assert.Equal(codigoEsperado, response.StatusCode);
        Assert.Contains(mensajeEsperado, await response.Content.ReadAsStringAsync());
        Assert.Equal((10, 20), factory.Seguimiento.UltimosIds);
    }

    [Fact]
    public async Task Falla_inesperada_devuelve_500_sin_exponer_detalles()
    {
        await using var factory = new ApiFactory();
        factory.Seguimiento.Error = new InvalidOperationException("detalle interno");
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
        public ResultadoSeguirUsuario Resultado { get; set; }
        public Exception? Error { get; set; }
        public (int Seguidor, int Seguido) UltimosIds { get; private set; }
        public bool FueInvocado { get; private set; }

        public Task<ResultadoSeguirUsuario> SeguirUsuarioAsync(
            int idUsuario,
            int idUsuarioASeguir,
            CancellationToken cancellationToken = default)
        {
            FueInvocado = true;
            UltimosIds = (idUsuario, idUsuarioASeguir);

            return Error is null
                ? Task.FromResult(Resultado)
                : Task.FromException<ResultadoSeguirUsuario>(Error);
        }
    }
}
