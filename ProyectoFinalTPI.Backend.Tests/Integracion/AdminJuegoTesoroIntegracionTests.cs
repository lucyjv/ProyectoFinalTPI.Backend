using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public sealed class AdminJuegoTesoroIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private const string Api = "/api/admin/juegos/tesoro";
    private static readonly GuardarUbicacionTesoroDto Nueva = new("Obelisco", "Una aguja señala el cielo porteño.", -34.6037, -58.3816, 18, true);
    private static readonly VistaMapaTesoroDto VistaInicial = new() { Zoom = 18, MinLatitud = -34.596, MaxLatitud = -34.595, MinLongitud = -58.379, MaxLongitud = -58.378 };

    [Fact]
    public async Task QueSoloUnAdministradorPuedaLeerYModificarCatalogoProgramacionYRetoActual()
    {
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: new Reloj());
        using var cliente = factory.CreateClient();
        foreach (var estado in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
        {
            Assert.Equal(estado, (await cliente.GetAsync(Api)).StatusCode);
            Assert.Equal(estado, (await cliente.PostAsJsonAsync(Api + "/ubicaciones", Nueva)).StatusCode);
            Assert.Equal(estado, (await cliente.PutAsJsonAsync(Api + "/ubicaciones/1", Nueva)).StatusCode);
            Assert.Equal(estado, (await cliente.PutAsJsonAsync(Api + "/semanas/2026-11-29", new ProgramarTesoroDto(1))).StatusCode);
            Assert.Equal(estado, (await cliente.DeleteAsync(Api + "/semanas/2026-11-29")).StatusCode);
            Assert.Equal(estado, (await cliente.PutAsJsonAsync(Api + "/actual/1", new EditarRetoTesoroDto("Otro", "Otra pista", 0, 0, 18))).StatusCode);
            if (estado == HttpStatusCode.Unauthorized) await Registrar(factory, cliente);
        }
    }

    [Fact]
    public async Task QueElCatalogoSeEditeSinMoverElActualYQueLaProgramacionSeApliqueElDomingo()
    {
        var reloj = new Reloj();
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: reloj);
        using var cliente = factory.CreateClient();
        await Registrar(factory, cliente, true);
        var panel = (await cliente.GetFromJsonAsync<PanelTesoroAdminDto>(Api))!;
        Assert.Single(panel.Ubicaciones);
        Assert.Equal(13, panel.Semanas.Count);
        Assert.NotNull(panel.Actual);
        var creada = await cliente.PostAsJsonAsync(Api + "/ubicaciones", Nueva);
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var ubicacion = (await creada.Content.ReadFromJsonAsync<UbicacionTesoroDto>())!;
        Assert.NotEqual(panel.Ubicaciones[0].Id, ubicacion.Id);
        var original = panel.Ubicaciones[0];
        Assert.Equal(HttpStatusCode.OK, (await cliente.PutAsJsonAsync(Api + $"/ubicaciones/{original.Id}",
            new GuardarUbicacionTesoroDto("Nombre editado", "Pista futura editada", 0, 0, 19, false))).StatusCode);
        var actual = (await cliente.GetFromJsonAsync<RetoTesoroDto>("/api/juegos/tesoro/actual"))!;
        Assert.Equal(panel.Actual!.Id, actual.Id);
        Assert.Equal(panel.Actual.Pista, actual.Pista);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PutAsJsonAsync(Api + "/semanas/2026-11-29", new ProgramarTesoroDto(ubicacion.Id))).StatusCode);
        reloj.Ahora = DateTimeOffset.Parse("2026-11-29T03:00:00Z");
        var siguiente = (await cliente.GetFromJsonAsync<RetoTesoroDto>("/api/juegos/tesoro/actual"))!;
        Assert.Equal(Nueva.Pista, siguiente.Pista);
        Assert.NotEqual(actual.Id, siguiente.Id);
    }

    [Fact]
    public async Task QueEditarElRetoVigenteConserveIdYHallazgosPeroMuevaElTesoroParaLosDemas()
    {
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: new Reloj());
        using var admin = factory.CreateClient();
        await Registrar(factory, admin, true);
        var panel = (await admin.GetFromJsonAsync<PanelTesoroAdminDto>(Api))!;
        var reto = panel.Actual!;
        var captura = await admin.PostAsJsonAsync($"/api/juegos/tesoro/{reto.Id}/hallazgo", VistaInicial);
        Assert.Equal(HttpStatusCode.OK, captura.StatusCode);
        var hallazgo = (await captura.Content.ReadFromJsonAsync<HallazgoTesoroDto>())!;
        var edicion = new EditarRetoTesoroDto(Nueva.Nombre, Nueva.Pista, Nueva.Latitud, Nueva.Longitud, 19);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(Api + $"/actual/{reto.Id}", edicion)).StatusCode);
        var actualizado = (await admin.GetFromJsonAsync<RetoTesoroDto>("/api/juegos/tesoro/actual"))!;
        Assert.Equal(reto.Id, actualizado.Id);
        Assert.Equal(hallazgo.HalladoUtc, actualizado.HalladoUtc);
        Assert.Equal(Nueva.Pista, actualizado.Pista);
        using var otro = factory.CreateClient();
        await Registrar(factory, otro);
        Assert.Equal(HttpStatusCode.BadRequest, (await otro.PostAsJsonAsync($"/api/juegos/tesoro/{reto.Id}/hallazgo", VistaInicial)).StatusCode);
        var nuevaVista = new VistaMapaTesoroDto { Zoom = 19, MinLatitud = -34.604, MaxLatitud = -34.603, MinLongitud = -58.382, MaxLongitud = -58.381 };
        Assert.Equal(HttpStatusCode.OK, (await otro.PostAsJsonAsync($"/api/juegos/tesoro/{reto.Id}/hallazgo", nuevaVista)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.RetosTesoro.CountAsync());
        Assert.Equal(2, await db.HallazgosTesoro.CountAsync());
        Assert.Equal(panel.Ubicaciones[0].Pista, (await db.UbicacionesTesoro.SingleAsync()).Pista);
    }

    [Fact]
    public async Task QueSePuedaPausarUnaSemanaYRestablecerRotacionSinVolverACrearDatos()
    {
        var reloj = new Reloj();
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: reloj);
        using var cliente = factory.CreateClient();
        await Registrar(factory, cliente, true);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PutAsJsonAsync(Api + "/semanas/2026-11-29", new ProgramarTesoroDto(null))).StatusCode);
        var panel = (await cliente.GetFromJsonAsync<PanelTesoroAdminDto>(Api))!;
        Assert.Equal("pausada", panel.Semanas[1].Modo);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.DeleteAsync(Api + "/semanas/2026-11-29")).StatusCode);
        Assert.Equal("automatica", (await cliente.GetFromJsonAsync<PanelTesoroAdminDto>(Api))!.Semanas[1].Modo);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PutAsJsonAsync(Api + "/semanas/2026-11-29", new ProgramarTesoroDto(null))).StatusCode);
        reloj.Ahora = DateTimeOffset.Parse("2026-11-29T03:00:00Z");
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.GetAsync("/api/juegos/tesoro/actual")).StatusCode);
    }

    [Fact]
    public async Task QueRechaceSemanasCerradasUbicacionesInactivasYDatosInvalidos()
    {
        var reloj = new Reloj();
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: reloj);
        using var cliente = factory.CreateClient();
        await Registrar(factory, cliente, true);
        foreach (var semana in new[] { "2026-11-22", "2026-11-15", "2026-11-30", "2027-11-28" })
            Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PutAsJsonAsync(Api + $"/semanas/{semana}", new ProgramarTesoroDto(1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.PutAsJsonAsync(Api + "/semanas/2026-11-29", new ProgramarTesoroDto(99999))).StatusCode);
        foreach (var dto in new[] { Nueva with { Pista = " " }, Nueva with { Latitud = 86 }, Nueva with { Longitud = 181 }, Nueva with { ZoomMinimo = 16 }, Nueva with { ZoomMinimo = 20 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync(Api + "/ubicaciones", dto)).StatusCode);
        var creada = await cliente.PostAsJsonAsync(Api + "/ubicaciones", Nueva with { Activa = false });
        var inactiva = (await creada.Content.ReadFromJsonAsync<UbicacionTesoroDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PutAsJsonAsync(Api + "/semanas/2026-11-29", new ProgramarTesoroDto(inactiva.Id))).StatusCode);
        var actual = (await cliente.GetFromJsonAsync<PanelTesoroAdminDto>(Api))!.Actual!;
        reloj.Ahora = DateTimeOffset.Parse("2026-11-29T03:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PutAsJsonAsync(Api + $"/actual/{actual.Id}", new EditarRetoTesoroDto(Nueva.Nombre, Nueva.Pista, Nueva.Latitud, Nueva.Longitud, 18))).StatusCode);
    }

    private static async Task Registrar(PublicacionesApiFactory factory, HttpClient cliente, bool admin = false)
    {
        var nombre = "admin_tesoro_" + Guid.NewGuid().ToString("N")[..8];
        var email = nombre + "@example.test";
        const string clave = "TesoroSeguro123!";
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/registro", new RegistroDto { Username = nombre, Email = email, Contraseña = clave });
        respuesta.EnsureSuccessStatusCode();
        var sesion = (await respuesta.Content.ReadFromJsonAsync<AuthRespuestaDto>())!;
        if (admin)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Usuarios.Where(u => u.Id == sesion.UsuarioId).ExecuteUpdateAsync(s => s.SetProperty(u => u.EsAdmin, true));
            respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Contraseña = clave });
            respuesta.EnsureSuccessStatusCode();
            sesion = (await respuesta.Content.ReadFromJsonAsync<AuthRespuestaDto>())!;
        }
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion.Token);
    }

    private sealed class Reloj : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = DateTimeOffset.Parse("2026-11-23T15:00:00Z");
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
