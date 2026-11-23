using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public sealed class JuegoTesoroIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private static VistaMapaTesoroDto Vista(double zoom = 18) => new()
    { Zoom = zoom, MinLatitud = -34.596, MaxLatitud = -34.595, MinLongitud = -58.379, MaxLongitud = -58.378 };

    private PublicacionesApiFactory Factory(TimeProvider? reloj = null) => new(postgres, timeProvider: reloj)
    {
        ConfigurarServiciosAdicionales = servicios => servicios.Configure<JuegoTesoroOpciones>(o =>
        {
            o.Habilitado = true;
            o.SemanaBase = new(2026, 11, 22);
            o.Ubicaciones = new() { new() { Clave = "retiro", Pista = "Un Libertador a caballo guarda un secreto.", NombreHallazgo = "Plaza San Martín, Retiro", Latitud = -34.59558, Longitud = -58.3785, ZoomMinimo = 18 },
                new() { Clave = "otra", Pista = "El próximo secreto.", NombreHallazgo = "Otro lugar", Latitud = -34.6037, Longitud = -58.3816, ZoomMinimo = 18 } };
        })
    };

    [Fact]
    public async Task QueLaPistaNoReveleLaUbicacionYLasCapturasConcurrentesSeanUnicasPorUsuario()
    {
        var reloj = new Reloj(DateTimeOffset.Parse("2026-11-23T15:00:00Z"));
        await using var factory = Factory(reloj);
        using var cliente = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/juegos/tesoro/actual")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/juegos/tesoro/1/hallazgo", Vista())).StatusCode);
        var usuario = await Registrar(cliente);
        var respuesta = await cliente.GetAsync("/api/juegos/tesoro/actual");
        var json = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("latitud", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("longitud", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nombre", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
        var reto = (await respuesta.Content.ReadFromJsonAsync<RetoTesoroDto>())!;
        Assert.Null(reto.HalladoUtc);
        Assert.Equal(HttpStatusCode.NoContent, (await Visible(cliente, Vista(17.99))).StatusCode);
        var lejos = Vista(); lejos.MinLongitud = -60; lejos.MaxLongitud = -59;
        Assert.Equal(HttpStatusCode.NoContent, (await Visible(cliente, lejos)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync($"/api/juegos/tesoro/{reto.Id}/hallazgo", lejos)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Visible(cliente, Vista())).StatusCode);

        var capturas = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => cliente.PostAsJsonAsync($"/api/juegos/tesoro/{reto.Id}/hallazgo", Vista())));
        Assert.All(capturas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var hallazgos = await Task.WhenAll(capturas.Select(r => r.Content.ReadFromJsonAsync<HallazgoTesoroDto>()));
        Assert.Single(hallazgos.Select(h => h!.HalladoUtc).Distinct());
        Assert.Equal(HttpStatusCode.NoContent, (await Visible(cliente, Vista())).StatusCode);
        using var otraPestana = factory.CreateClient();
        otraPestana.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", usuario.Token);
        Assert.NotNull((await otraPestana.GetFromJsonAsync<RetoTesoroDto>("/api/juegos/tesoro/actual"))!.HalladoUtc);

        using var otroUsuario = factory.CreateClient();
        await Registrar(otroUsuario);
        Assert.Equal(HttpStatusCode.OK, (await Visible(otroUsuario, Vista())).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.RetosTesoro.CountAsync());
        Assert.Equal(1, await db.HallazgosTesoro.CountAsync());
        Assert.Equal(usuario.UsuarioId, (await db.HallazgosTesoro.SingleAsync()).UsuarioId);
    }

    [Fact]
    public async Task QueElCambioDeSemanaCreeOtroRetoYNoAcepteLaPistaAnterior()
    {
        var reloj = new Reloj(DateTimeOffset.Parse("2026-11-29T02:59:59Z"));
        await using var factory = Factory(reloj);
        using var cliente = factory.CreateClient();
        await Registrar(cliente);
        var primero = (await cliente.GetFromJsonAsync<RetoTesoroDto>("/api/juegos/tesoro/actual"))!;
        var capturado = await cliente.PostAsJsonAsync($"/api/juegos/tesoro/{primero.Id}/hallazgo", Vista());
        Assert.Equal(HttpStatusCode.OK, capturado.StatusCode);
        reloj.Ahora = reloj.Ahora.AddSeconds(1);
        var siguiente = (await cliente.GetFromJsonAsync<RetoTesoroDto>("/api/juegos/tesoro/actual"))!;
        Assert.NotEqual(primero.Id, siguiente.Id);
        Assert.Equal(primero.FinUtc, siguiente.InicioUtc);
        Assert.Equal("El próximo secreto.", siguiente.Pista);
        Assert.Null(siguiente.HalladoUtc);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync($"/api/juegos/tesoro/{primero.Id}/hallazgo", Vista())).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.RetosTesoro.CountAsync());
        Assert.Equal(1, await db.HallazgosTesoro.CountAsync());
    }

    [Fact]
    public async Task QueConsultasSimultaneasCongelenUnaSolaPistaSemanal()
    {
        await using var factory = Factory(new Reloj(DateTimeOffset.Parse("2026-11-23T15:00:00Z")));
        using var cliente = factory.CreateClient();
        await Registrar(cliente);
        var retos = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => cliente.GetFromJsonAsync<RetoTesoroDto>("/api/juegos/tesoro/actual")));
        Assert.Single(retos.Select(r => r!.Id).Distinct());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.RetosTesoro.CountAsync());
        Assert.Empty(await db.HallazgosTesoro.ToListAsync());
    }

    private static Task<HttpResponseMessage> Visible(HttpClient cliente, VistaMapaTesoroDto v) => cliente.GetAsync(
        FormattableString.Invariant($"/api/juegos/tesoro/visible?zoom={v.Zoom}&minLatitud={v.MinLatitud}&maxLatitud={v.MaxLatitud}&minLongitud={v.MinLongitud}&maxLongitud={v.MaxLongitud}"));

    private static async Task<AuthRespuestaDto> Registrar(HttpClient cliente)
    {
        var nombre = "tesoro_" + Guid.NewGuid().ToString("N")[..10];
        using var respuesta = await cliente.PostAsJsonAsync("/api/auth/registro", new RegistroDto
        { Username = nombre, Email = nombre + "@example.test", Contraseña = "TesoroSeguro123!" });
        respuesta.EnsureSuccessStatusCode();
        var usuario = (await respuesta.Content.ReadFromJsonAsync<AuthRespuestaDto>())!;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", usuario.Token);
        return usuario;
    }

    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
