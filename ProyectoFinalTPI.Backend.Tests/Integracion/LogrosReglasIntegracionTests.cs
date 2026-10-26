using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class LogrosReglasIntegracionTests(PostgisFixture postgres, Neo4jLogrosFixture neo4j)
    : IClassFixture<PostgisFixture>, IClassFixture<Neo4jLogrosFixture>
{
    [Fact]
    public async Task CuentaNuevaNoDesbloqueaLogrosNiHeredaLosDeOtraCuentaAlRegistrarseOIngresar()
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        var anterior = await CrearUsuario(client, "anterior");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Logros_Usuarios.Add(new Logros_Usuarios
            {
                UsuarioId = anterior.UsuarioId, LogroId = 2, FechaDesbloqueo = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        Assert.True((await Perfil(client, anterior.UsuarioId)).Logros.Single(logro => logro.Id == 2).Desbloqueado);

        var nueva = await CrearUsuario(client, "nueva");
        Assert.Empty(nueva.LogrosNuevos);
        var perfil = await Perfil(client, nueva.UsuarioId);
        Assert.Equal(nueva.UsuarioId, perfil.Id);
        Assert.Equal(17, perfil.Logros.Count);
        Assert.All(perfil.Logros, logro =>
        {
            Assert.False(logro.Desbloqueado);
            Assert.Null(logro.FechaDesbloqueo);
        });
        Assert.DoesNotContain(perfil.Logros, logro => logro.Titulo == "Corazón abierto");
        Assert.Empty((await Login(client, "nueva")).LogrosNuevos);
        Assert.All((await Perfil(client, nueva.UsuarioId)).Logros, logro => Assert.False(logro.Desbloqueado));
        using var verificacion = factory.Services.CreateScope();
        var contexto = verificacion.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await contexto.Logros_Usuarios.AnyAsync(logro => logro.UsuarioId == nueva.UsuarioId));
        Assert.Equal(1, await contexto.Logros_Usuarios.CountAsync(logro => logro.UsuarioId == anterior.UsuarioId));
    }

    [Fact]
    public async Task LoginCuentaUnIngresoPorDiaDesbloqueaTresYSieteYNoCuentaClavesInvalidas()
    {
        var clock = new Reloj { Ahora = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero) };
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: clock);
        using var client = factory.CreateClient();
        var user = await CrearUsuario(client, "racha");
        for (var day = 2; day <= 7; day++)
        {
            clock.Ahora = clock.Ahora.AddDays(1);
            var ingreso = await Login(client, "racha");
            Assert.Equal(day == 3 ? new[] { 14 } : day == 7 ? new[] { 19 } : Array.Empty<int>(), ingreso.LogrosNuevos.Select(logro => logro.Id));
            if (day == 2) Assert.Empty((await Login(client, "racha")).LogrosNuevos);
            var profile = await Perfil(client, user.UsuarioId);
            Assert.Equal(day >= 3, profile.Logros.Single(logro => logro.Id == 14).Desbloqueado);
            Assert.Equal(day >= 7, profile.Logros.Single(logro => logro.Id == 19).Desbloqueado);
        }
        clock.Ahora = clock.Ahora.AddDays(1);
        var failed = await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = "racha@example.test", Contraseña = "incorrecta" });
        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        using var scope = factory.Services.CreateScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Usuario_Personales.SingleAsync();
        Assert.Equal(7, persisted.RachaDiasActual);
        Assert.Equal(clock.Ahora.AddDays(-1).UtcDateTime, persisted.UltimoIngresoUtc);
    }

    [Fact]
    public async Task SaltarUnDiaReiniciaLaRacha()
    {
        var clock = new Reloj { Ahora = new DateTimeOffset(2026, 10, 1, 23, 59, 0, TimeSpan.Zero) };
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: clock);
        using var client = factory.CreateClient();
        var user = await CrearUsuario(client, "salto");
        clock.Ahora = clock.Ahora.AddMinutes(2);
        await Login(client, "salto");
        clock.Ahora = clock.Ahora.AddDays(2);
        await Login(client, "salto");
        using var scope = factory.Services.CreateScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Usuario_Personales.SingleAsync();
        Assert.Equal(1, persisted.RachaDiasActual);
        Assert.False((await Perfil(client, user.UsuarioId)).Logros.Single(logro => logro.Id == 14).Desbloqueado);
    }

    [Fact]
    public async Task LinceSeEntregaEnLaPublicacionQuinceYNoEnLaCatorce()
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        var user = await CrearUsuario(client, "lince");
        for (var i = 0; i < 14; i++) await CrearRecuerdo(client);
        Assert.False((await Perfil(client, user.UsuarioId)).Logros.Single(logro => logro.Id == 13).Desbloqueado);
        await CrearRecuerdo(client);
        Assert.True((await Perfil(client, user.UsuarioId)).Logros.Single(logro => logro.Id == 13).Desbloqueado);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(15, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Publicaciones.CountAsync());
    }

    [Fact]
    public async Task CronistaRequiereCincoRecuerdosCercanosYLaUbicacionActualCompleta()
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        var user = await CrearUsuario(client, "cronista");
        for (var i = 0; i < 4; i++) await CrearRecuerdo(client, gps: true);
        Assert.False((await Perfil(client, user.UsuarioId)).Logros.Single(logro => logro.Id == 7).Desbloqueado);
        await CrearRecuerdo(client, gps: true, latitud: -35.6);
        Assert.False((await Perfil(client, user.UsuarioId)).Logros.Single(logro => logro.Id == 7).Desbloqueado);
        await CrearRecuerdo(client);
        Assert.False((await Perfil(client, user.UsuarioId)).Logros.Single(logro => logro.Id == 7).Desbloqueado);
        using var partial = Formulario();
        partial.Add(new StringContent("-34.6"), "GpsLatitud");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/publicaciones", partial)).StatusCode);
        await CrearRecuerdo(client, gps: true);
        Assert.True((await Perfil(client, user.UsuarioId)).Logros.Single(logro => logro.Id == 7).Desbloqueado);
    }

    [Fact]
    public async Task PuntuacionesPersistenYDesbloqueanPopularLeyendaYGenerosoEnSusUmbrales()
    {
        await using var factory = new PublicacionesApiFactory(postgres, neo4j.Driver);
        using var client = factory.CreateClient();
        var author = await CrearUsuario(client, "autor");
        var target = await CrearRecuerdo(client);
        var voter = await CrearUsuario(client, "votante");
        List<int> others;
        List<int> posts;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = Enumerable.Range(0, 19).Select(i => new Usuario_Personal { Username = $"otro{i}", Email = $"otro{i}@example.test" }).ToList();
            db.Usuarios.AddRange(users);
            var place = await db.Lugares.FirstAsync();
            var publications = Enumerable.Range(0, 9).Select(i => new Publicacion
            {
                UsuarioId = author.UsuarioId, Titulo = $"Recuerdo {i}", Descripcion = "Texto", LugarId = place.Id,
                Fecha = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), UrlMultimedia = "https://media.example/test"
            }).ToList();
            db.Publicaciones.AddRange(publications);
            await db.SaveChangesAsync();
            others = users.Select(user => user.Id).ToList();
            posts = publications.Select(post => post.Id).ToList();
        }
        await SembrarPuntos(others.Take(3), target);
        Assert.Equal(19, (await Puntuar(client, target, 4)).PuntosTotales);
        Assert.False((await Perfil(client, author.UsuarioId)).Logros.Single(logro => logro.Id == 10).Desbloqueado);
        Assert.Equal(20, (await Puntuar(client, target, 5)).PuntosTotales);
        Assert.True((await Perfil(client, author.UsuarioId)).Logros.Single(logro => logro.Id == 10).Desbloqueado);
        Assert.Null((await Puntuar(client, target, 5)).MiPuntuacion);
        await Puntuar(client, target, 5);
        await SembrarPuntos(others.Skip(3), target);
        Assert.Equal(99, (await Puntuar(client, target, 4)).PuntosTotales);
        Assert.False((await Perfil(client, author.UsuarioId)).Logros.Single(logro => logro.Id == 16).Desbloqueado);
        Assert.Equal(100, (await Puntuar(client, target, 5)).PuntosTotales);
        Assert.True((await Perfil(client, author.UsuarioId)).Logros.Single(logro => logro.Id == 16).Desbloqueado);
        foreach (var post in posts.Take(8)) await Puntuar(client, post, 5);
        await Puntuar(client, posts.Last(), 4);
        Assert.False((await Perfil(client, voter.UsuarioId)).Logros.Single(logro => logro.Id == 11).Desbloqueado);
        await Puntuar(client, posts.Last(), 5);
        Assert.True((await Perfil(client, voter.UsuarioId)).Logros.Single(logro => logro.Id == 11).Desbloqueado);
        var detail = await client.GetFromJsonAsync<PublicacionDetalleDto>($"/api/publicaciones/{target}");
        Assert.Equal(5, detail!.MiPuntuacion);
        Assert.Equal(100, detail.PuntosTotales);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/publicaciones/{target}/puntuacion", new { puntaje = 6 })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", author.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/publicaciones/{target}/puntuacion", new { puntaje = 5 })).StatusCode);
    }

    [Theory]
    [InlineData("repost")]
    [InlineData("compartir")]
    [InlineData("citar")]
    public async Task ViajeroCuentaRepostsDeNeo4jYCitasSinContarRepetidosPropiosNiDeOtrosUsuarios(string accionFinal)
    {
        // El grafo pertenece exclusivamente al contenedor temporal de esta clase.
        await using (var session = neo4j.Driver.AsyncSession())
            await (await session.RunAsync("MATCH (n) DETACH DELETE n")).ConsumeAsync();

        await using var factory = new PublicacionesApiFactory(postgres, neo4j.Driver);
        using var client = factory.CreateClient();
        var author = await CrearUsuario(client, "autor_viajero");
        var user = await CrearUsuario(client, "viajero");
        List<int> ajenas;
        int propia;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var place = new Lugar
            {
                Nombre = "Buenos Aires", Coordenadas = new NetTopologySuite.Geometries.Point(-58.4, -34.6) { SRID = 4326 }
            };
            var publicaciones = Enumerable.Range(0, 11).Select(i => new Publicacion
            {
                UsuarioId = i == 10 ? user.UsuarioId : author.UsuarioId,
                Titulo = $"Recuerdo {i}", Descripcion = "Una historia", Lugar = place,
                Fecha = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }).ToList();
            db.Publicaciones.AddRange(publicaciones);
            await db.SaveChangesAsync();
            ajenas = publicaciones.Take(10).Select(publicacion => publicacion.Id).ToList();
            propia = publicaciones.Last().Id;
        }

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/publicaciones/{ajenas[0]}/repost", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/publicaciones/{propia}/repost", null)).StatusCode);
        foreach (var id in ajenas.Skip(1).Take(8))
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/publicaciones/{id}/repost", null)).StatusCode);

        var other = await CrearUsuario(client, "otro_viajero");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/publicaciones/{ajenas[9]}/repost", null)).StatusCode);
        await Login(client, "viajero");
        Assert.False((await Perfil(client, user.UsuarioId)).Logros.Single(logro => logro.Id == 20).Desbloqueado);

        var response = accionFinal switch
        {
            "compartir" => await client.PostAsJsonAsync("/api/publicaciones/compartir",
                new { publicacionOriginalId = ajenas[9], autorId = user.UsuarioId, descripcion = "" }),
            "citar" => await client.PostAsJsonAsync($"/api/publicaciones/{ajenas[9]}/citar",
                new { descripcion = "Mi cita del recuerdo" }),
            _ => await client.PostAsync($"/api/publicaciones/{ajenas[9]}/repost", null)
        };
        Assert.Equal(accionFinal == "repost" ? HttpStatusCode.OK : HttpStatusCode.Created, response.StatusCode);
        Assert.True((await Perfil(client, user.UsuarioId)).Logros.Single(logro => logro.Id == 20).Desbloqueado);
        Assert.False((await Perfil(client, other.UsuarioId)).Logros.Single(logro => logro.Id == 20).Desbloqueado);

        using var verificacion = factory.Services.CreateScope();
        var contexto = verificacion.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(accionFinal == "citar" ? 12 : 11, await contexto.Publicaciones.CountAsync());
        Assert.Equal(1, await contexto.Logros_Usuarios.CountAsync(logro => logro.UsuarioId == user.UsuarioId && logro.LogroId == 20));
    }

    private async Task SembrarPuntos(IEnumerable<int> users, int post)
    {
        await using var session = neo4j.Driver.AsyncSession();
        await (await session.RunAsync("""
            UNWIND $users AS id
            MERGE (u:Usuario {postgresId: id})
            MERGE (p:Publicacion {postgresId: $post})
            MERGE (u)-[r:PUNTUO]->(p) SET r.puntaje = 5
            """, new { users = users.ToArray(), post })).ConsumeAsync();
    }
    private static async Task<AuthRespuestaDto> CrearUsuario(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/auth/registro", new RegistroDto { Username = name, Email = name + "@example.test", Contraseña = "PruebaLogros123!" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<AuthRespuestaDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        return user;
    }
    private static async Task<AuthRespuestaDto> Login(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = name + "@example.test", Contraseña = "PruebaLogros123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<AuthRespuestaDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        return user;
    }
    private static async Task<UsuarioPerfilDto> Perfil(HttpClient client, int id) => (await client.GetFromJsonAsync<UsuarioPerfilDto>($"/api/usuario/{id}/perfil"))!;
    private static async Task<PuntuacionPublicacionDto> Puntuar(HttpClient client, int id, int score)
    {
        var response = await client.PutAsJsonAsync($"/api/publicaciones/{id}/puntuacion", new { puntaje = score });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PuntuacionPublicacionDto>())!;
    }
    private static MultipartFormDataContent Formulario(double latitud = -34.6)
    {
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in new Dictionary<string, string> {
            ["Titulo"] = "Recuerdo", ["Descripcion"] = "Una historia", ["Fecha"] = "1995-01-01T00:00:00Z",
            ["Categoria"] = "Lugares", ["NombreLugar"] = "Buenos Aires", ["Latitud"] = latitud.ToString(CultureInfo.InvariantCulture),
            ["Longitud"] = "-58.4", ["TipoMultimedia"] = "Foto", ["UrlArchivo"] = "https://media.example/test", ["AutorId"] = "1"
        }) form.Add(new StringContent(value), key);
        return form;
    }
    private static async Task<int> CrearRecuerdo(HttpClient client, bool gps = false, double latitud = -34.6)
    {
        using var form = Formulario(latitud);
        if (gps) { form.Add(new StringContent("-34.6"), "GpsLatitud"); form.Add(new StringContent("-58.4"), "GpsLongitud"); }
        var response = await client.PostAsync("/api/publicaciones", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }
    private sealed class Reloj : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; }
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
