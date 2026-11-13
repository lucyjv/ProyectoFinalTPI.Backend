using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

public class LimitesYAdjuntosIntegracionTests(PostgisFixture postgres, Neo4jLogrosFixture neo4j)
    : IClassFixture<PostgisFixture>, IClassFixture<Neo4jLogrosFixture>
{
    [Fact]
    public async Task CincoIncluyenElPrimeroYElGrupo_BorrarNoDevuelveCupo_ElDomingoSeRenueva()
    {
        var reloj = new RelojCategoria(DateTimeOffset.Parse("2026-11-15T02:59:59Z"));
        await using var factory = new PublicacionesApiFactory(postgres, neo4j.Driver)
        {
            ConfigurarServiciosAdicionales = s => s.AddScoped<ICategoriaServicio>(sp =>
                new CategoriaServicio(sp.GetRequiredService<ProyectoFinalTPI.Backend.Interfaces.Repositorio.ICategoriaRepositorio>(), reloj))
        };
        using var client = factory.CreateClient();
        var usuario = await Registrar(client);
        var primero = await Publicar(client, usuario.UsuarioId);
        Assert.Equal("Nostalgico", (await Estado(client)).Categoria.Codigo);
        Assert.Equal(4, (await Estado(client)).PublicacionesRestantes);
        using var grupo = await client.PostAsJsonAsync("/api/grupos", new { nombre = "Memorias", descripcion = "Historias", decada = 1990 });
        Assert.Equal(HttpStatusCode.Created, grupo.StatusCode);
        var grupoId = (await grupo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        using var formGrupo = Formulario(usuario.UsuarioId);
        using var respuestaGrupo = await client.PostAsync($"/api/grupos/{grupoId}/recuerdos", formGrupo);
        Assert.True(respuestaGrupo.StatusCode == HttpStatusCode.Created, await respuestaGrupo.Content.ReadAsStringAsync());
        for (var i = 0; i < 3; i++) await Publicar(client, usuario.UsuarioId);
        await Publicar(client, usuario.UsuarioId, esperado: HttpStatusCode.TooManyRequests);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/publicaciones/{primero}")).StatusCode);
        Assert.Equal(5, (await Estado(client)).PublicacionesConsumidas);
        await Publicar(client, usuario.UsuarioId, esperado: HttpStatusCode.TooManyRequests);
        reloj.Ahora = reloj.Ahora.AddSeconds(1);
        Assert.Equal(5, (await Estado(client)).PublicacionesRestantes);
        await Publicar(client, usuario.UsuarioId);
        Assert.Equal(4, (await Estado(client)).PublicacionesRestantes);
    }

    [Fact]
    public async Task DoceCreacionesConcurrentesNoSuperanElCupoDeCinco()
    {
        await using var factory = new PublicacionesApiFactory(postgres, neo4j.Driver);
        using var client = factory.CreateClient();
        var usuario = await Registrar(client);
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            using var form = Formulario(usuario.UsuarioId);
            using var respuesta = await client.PostAsync("/api/publicaciones", form);
            return respuesta.StatusCode;
        }));
        Assert.Equal(5, respuestas.Count(e => e == HttpStatusCode.Created));
        Assert.Equal(7, respuestas.Count(e => e == HttpStatusCode.TooManyRequests));
        Assert.Equal(5, (await Estado(client)).PublicacionesConsumidas);
    }

    [Fact]
    public async Task MnemosineGuardaCuatroAdjuntosMixtosYMusica_NostalgicoRechazaDos_YCincoSonInvalidos()
    {
        await using var factory = new PublicacionesApiFactory(postgres, neo4j.Driver);
        using var client = factory.CreateClient();
        var usuario = await Registrar(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Set<Usuario_Personal>().Where(u => u.Id == usuario.UsuarioId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Reputacion, ReputacionEnum.Mnnemónide));
        }
        var id = await Publicar(client, usuario.UsuarioId, 4, musica: true);
        var detalle = (await client.GetFromJsonAsync<PublicacionDetalleDto>($"/api/publicaciones/{id}"))!;
        Assert.Equal(4, detalle.Adjuntos.Count);
        Assert.Equal(new[] { MultimediaEnum.Foto, MultimediaEnum.Video, MultimediaEnum.Foto, MultimediaEnum.Video }, detalle.Adjuntos.Select(a => a.Tipo));
        Assert.Equal("1234567890123456789012", detalle.Musica!.SpotifyId);
        var resumen = (await client.GetFromJsonAsync<PublicacionResumenDto>($"/api/publicaciones/{id}/resumen"))!;
        Assert.Equal(4, resumen.Adjuntos.Count);
        Assert.Equal(4, factory.Media.Calls);
        await Publicar(client, usuario.UsuarioId, 5, esperado: HttpStatusCode.BadRequest);
        Assert.Equal(4, factory.Media.Calls);
        var otro = await Registrar(client);
        await Publicar(client, otro.UsuarioId);
        await Publicar(client, otro.UsuarioId, 2, esperado: HttpStatusCode.Forbidden);
        await Publicar(client, otro.UsuarioId, musica: true, esperado: HttpStatusCode.Forbidden);
        Assert.Equal(4, factory.Media.Calls);
        Assert.Equal(1, (await Estado(client)).PublicacionesConsumidas);
    }

    [Fact]
    public async Task TreintaPuntosDanAedoYSieteRecuerdos_LikesRepostsYEtiquetasNoConsumenCupo()
    {
        await using var factory = new PublicacionesApiFactory(postgres, neo4j.Driver);
        using var client = factory.CreateClient();
        var autor = await Registrar(client);
        var recuerdo = await Publicar(client, autor.UsuarioId);
        var lectores = new List<AuthRespuestaDto>();
        for (var i = 0; i < 6; i++)
        {
            var lector = await Registrar(client);
            lectores.Add(lector);
            using var denegado = await client.PutAsJsonAsync($"/api/publicaciones/{recuerdo}/puntuacion", new { puntaje = 5 });
            Assert.Equal(HttpStatusCode.Forbidden, denegado.StatusCode);
            await Publicar(client, lector.UsuarioId);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/publicaciones/{recuerdo}/like", null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/publicaciones/{recuerdo}/puntuacion", new { puntaje = 5 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/publicaciones/{recuerdo}/repost", null)).StatusCode);
            Assert.Equal(1, (await Estado(client)).PublicacionesConsumidas);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", autor.Token);
        Assert.Equal("Aedo", (await Estado(client)).Categoria.Codigo);
        Assert.Equal(6, (await Estado(client)).PublicacionesRestantes);
        for (var i = 0; i < 6; i++) await Publicar(client, autor.UsuarioId);
        await Publicar(client, autor.UsuarioId, esperado: HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ParaVosPriorizaLaCategoriaDelAutorEnLaVersionActualDelFeed()
    {
        await using (var session = neo4j.Driver.AsyncSession())
            await (await session.RunAsync("MATCH (n) DETACH DELETE n")).ConsumeAsync();
        await using var factory = new PublicacionesApiFactory(postgres, neo4j.Driver);
        using var client = factory.CreateClient();
        var aedo = await Registrar(client);
        var recuerdoAedo = await Publicar(client, aedo.UsuarioId);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<Usuario_Personal>()
                .Where(u => u.Id == aedo.UsuarioId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Reputacion, ReputacionEnum.Aedo));
        var nostalgico = await Registrar(client);
        var recuerdoNostalgico = await Publicar(client, nostalgico.UsuarioId);
        await Registrar(client);
        var pagina = (await client.GetFromJsonAsync<ProyectoFinalTPI.Backend.Dtos.ResultadoPaginadoDto<PublicacionDetalleDto>>("/api/feed/para-vos?limite=20"))!;
        Assert.Equal(new[] { recuerdoAedo, recuerdoNostalgico }, pagina.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task ErranteConservaLikesYGuardados_PeroComentarPuntuarYCompartirRequierenElPrimerRecuerdo()
    {
        await using var factory = new PublicacionesApiFactory(postgres, neo4j.Driver);
        using var client = factory.CreateClient();
        var autor = await Registrar(client);
        var recuerdo = await Publicar(client, autor.UsuarioId);
        var cuenta = await Registrar(client);
        Assert.Equal("Errante", (await Estado(client)).Categoria.Codigo);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/publicaciones/{recuerdo}/like", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/publicaciones/{recuerdo}/guardar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/publicaciones/{recuerdo}/comentarios", new { contenido = "Mi comentario" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/publicaciones/{recuerdo}/puntuacion", new { puntaje = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/publicaciones/{recuerdo}/repost", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/publicaciones/{recuerdo}/citar", new { descripcion = "Mi mirada del recuerdo" })).StatusCode);
        Assert.Equal(0, (await Estado(client)).PublicacionesConsumidas);

        await Publicar(client, cuenta.UsuarioId);
        Assert.Equal("Nostalgico", (await Estado(client)).Categoria.Codigo);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/publicaciones/{recuerdo}/comentarios", new { contenido = "Mi comentario" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/publicaciones/{recuerdo}/puntuacion", new { puntaje = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/publicaciones/{recuerdo}/repost", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/publicaciones/{recuerdo}/citar", new { descripcion = "Mi mirada del recuerdo" })).StatusCode);
        Assert.Equal(1, (await Estado(client)).PublicacionesConsumidas);
        Assert.Equal(4, (await Estado(client)).PublicacionesRestantes);
    }

    [Fact]
    public async Task CrearRequiereSesionYUsaElAutorAutenticadoAunqueElFormularioIndiqueOtro()
    {
        await using var factory = new PublicacionesApiFactory(postgres, neo4j.Driver);
        using var client = factory.CreateClient();
        using (var anonimo = Formulario(123))
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/publicaciones", anonimo)).StatusCode);
        var autor = await Registrar(client);
        var otro = await Registrar(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", autor.Token);
        var recuerdo = await Publicar(client, otro.UsuarioId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(autor.UsuarioId, await db.Publicaciones.Where(p => p.Id == recuerdo).Select(p => p.UsuarioId).SingleAsync());
        Assert.Equal(1, (await Estado(client)).PublicacionesConsumidas);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otro.Token);
        Assert.Equal("Errante", (await Estado(client)).Categoria.Codigo);
        Assert.Equal(0, (await Estado(client)).PublicacionesConsumidas);
    }

    private static async Task<AuthRespuestaDto> Registrar(HttpClient client)
    {
        var nombre = "cuenta_" + Guid.NewGuid().ToString("N")[..10];
        using var respuesta = await client.PostAsJsonAsync("/api/auth/registro", new RegistroDto
        { Username = nombre, Email = nombre + "@example.test", Contraseña = "PruebaCategorias123!" });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var usuario = (await respuesta.Content.ReadFromJsonAsync<AuthRespuestaDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", usuario.Token);
        return usuario;
    }

    private static Task<EstadoCategoriaDto> Estado(HttpClient client) => client.GetFromJsonAsync<EstadoCategoriaDto>("/api/categorias-cuenta/me")!;

    private static MultipartFormDataContent Formulario(int usuarioId, int adjuntos = 0, bool musica = false)
    {
        var form = new MultipartFormDataContent();
        foreach (var campo in new Dictionary<string, string>
        {
            ["Titulo"] = "Recuerdo",
            ["Descripcion"] = "Historia",
            ["Fecha"] = "1995-04-15T00:00:00Z",
            ["Categoria"] = "Lugares",
            ["NombreLugar"] = "Buenos Aires",
            ["Latitud"] = "-34.6",
            ["Longitud"] = "-58.4",
            ["AutorId"] = usuarioId.ToString()
        }) form.Add(new StringContent(campo.Value), campo.Key);
        for (var i = 0; i < adjuntos; i++)
        {
            form.Add(new StringContent(i % 2 == 0 ? "Foto" : "Video"), $"Adjuntos[{i}].Tipo");
            if (i == 0) form.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), $"Adjuntos[{i}].Archivo", "foto.jpg");
            else form.Add(new StringContent($"https://media.example/archivo{i}"), $"Adjuntos[{i}].Url");
        }
        if (musica)
        {
            form.Add(new StringContent("1234567890123456789012"), "Musica.SpotifyId");
            form.Add(new StringContent("Tema"), "Musica.Titulo");
            form.Add(new StringContent("Artista"), "Musica.Artista");
        }
        return form;
    }

    private static async Task<int> Publicar(HttpClient client, int usuarioId, int adjuntos = 0, bool musica = false, HttpStatusCode esperado = HttpStatusCode.Created)
    {
        using var form = Formulario(usuarioId, adjuntos, musica);
        using var respuesta = await client.PostAsync("/api/publicaciones", form);
        Assert.True(respuesta.StatusCode == esperado, $"{respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        if (esperado != HttpStatusCode.Created) return 0;
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }
}
