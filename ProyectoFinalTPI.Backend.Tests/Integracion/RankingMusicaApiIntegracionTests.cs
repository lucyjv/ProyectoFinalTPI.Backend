using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.RankingMusica;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Services;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public sealed class RankingMusicaApiIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private const string Ranking = "/api/ranking-musica";
    private const string RankingAdmin = "/api/admin/ranking-musica";

    [Fact]
    public async Task QueLaApiControlePermisosYCupoYExpongaLosResultadosDespuesDelCierre()
    {
        // Arrange
        var reloj = new RelojCategoria(DateTimeOffset.UtcNow);
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: reloj);
        using var publico = factory.CreateClient();
        using var creador = factory.CreateClient();
        using var votante = factory.CreateClient();
        using var admin = factory.CreateClient();
        await Autenticar(factory, creador, "creador", ReputacionEnum.Mnnemónide);
        await Autenticar(factory, votante, "votante", ReputacionEnum.Nostalgico);
        await Autenticar(factory, admin, "administrador", ReputacionEnum.Errante, esAdmin: true);
        using var worker = ActivatorUtilities.CreateInstance<RankingMusicaWorker>(factory.Services);
        var primerCierre = await worker.EjecutarCiclo(default);
        var ronda = await publico.GetFromJsonAsync<RankingMusicaActualDto>($"{Ranking}/actual");
        Assert.NotNull(ronda);
        Assert.Equal(ronda.Cierre, primerCierre);
        Assert.Null(ronda.Saldo);

        // Act y Assert de permisos y edición.
        var propuesta = new CrearCandidaturaMusicaDto { Decada = 1980, Titulo = "Canción inicial", Artista = "Banda" };
        await VerificarEstado(await publico.PostAsJsonAsync($"{Ranking}/sugerencias", propuesta), HttpStatusCode.Unauthorized);
        await VerificarEstado(await votante.PostAsJsonAsync($"{Ranking}/sugerencias", propuesta), HttpStatusCode.Forbidden);
        await VerificarEstado(await creador.PostAsJsonAsync(RankingAdmin, propuesta), HttpStatusCode.Forbidden);
        var candidatura = await CrearCandidatura(creador, $"{Ranking}/sugerencias", propuesta);
        await CrearCandidatura(admin, RankingAdmin, new() { Decada = 1990, Titulo = "Canción del panel", Artista = "Banda" });
        await VerificarEstado(await creador.PostAsJsonAsync($"{Ranking}/sugerencias", propuesta), HttpStatusCode.Conflict);
        var datosEditados = new DatosCandidaturaMusicaDto { Titulo = "Canción corregida", Artista = "Banda" };
        await VerificarEstado(await votante.PutAsJsonAsync($"{Ranking}/{candidatura.Id}", datosEditados), HttpStatusCode.Forbidden);
        await VerificarEstado(await creador.PutAsJsonAsync($"{Ranking}/{candidatura.Id}", datosEditados), HttpStatusCode.OK);

        var otra = await CrearCandidatura(creador, $"{Ranking}/sugerencias",
            new() { Decada = 1980, Titulo = "Canción para retirar", Artista = "Banda" });
        await VerificarEstado(await votante.DeleteAsync($"{Ranking}/{otra.Id}"), HttpStatusCode.Forbidden);
        await VerificarEstado(await admin.DeleteAsync($"{Ranking}/{otra.Id}"), HttpStatusCode.NoContent);

        // Act y Assert del voto y del saldo.
        await VerificarEstado(await admin.PostAsJsonAsync($"{Ranking}/puntuacion",
            new { candidaturaId = candidatura.Id, puntos = 1 }), HttpStatusCode.Forbidden);
        await VerificarEstado(await votante.PostAsJsonAsync($"{Ranking}/puntuacion",
            new { candidaturaId = candidatura.Id, puntos = 0 }), HttpStatusCode.BadRequest);
        await VerificarEstado(await votante.PostAsJsonAsync($"{Ranking}/puntuacion",
            new { candidaturaId = otra.Id, puntos = 1 }), HttpStatusCode.NotFound);

        await VerificarEstado(await votante.PostAsJsonAsync($"{Ranking}/puntuacion",
            new { candidaturaId = candidatura.Id, puntos = 3 }), HttpStatusCode.OK);
        using var segundoVoto = await votante.PostAsJsonAsync($"{Ranking}/puntuacion",
            new { candidaturaId = candidatura.Id, puntos = 2 });
        Assert.Equal(HttpStatusCode.OK, segundoVoto.StatusCode);
        var voto = await segundoVoto.Content.ReadFromJsonAsync<VotoRankingMusicaDto>();
        Assert.NotNull(voto);
        Assert.Equal(5L, voto.PuntosTotales);
        Assert.Equal(0, voto.PuntosDiariosRestantes);
        using var sinSaldo = await votante.PostAsJsonAsync($"{Ranking}/puntuacion",
            new { candidaturaId = candidatura.Id, puntos = 1 });
        Assert.Equal(HttpStatusCode.TooManyRequests, sinSaldo.StatusCode);
        Assert.Equal(429, (await sinSaldo.Content.ReadFromJsonAsync<ProblemDetails>())?.Status);
        await VerificarEstado(await admin.PutAsJsonAsync($"{Ranking}/{candidatura.Id}", datosEditados), HttpStatusCode.Conflict);
        var actual = await votante.GetFromJsonAsync<RankingMusicaActualDto>($"{Ranking}/actual");
        Assert.Equal(0, actual?.Saldo?.PuntosRestantes);

        // Act y Assert del cierre y del acceso al histórico.
        reloj.Ahora = ronda.Cierre;
        await VerificarEstado(await publico.GetAsync($"{Ranking}/actual"), HttpStatusCode.NotFound);
        await VerificarEstado(await votante.PostAsJsonAsync($"{Ranking}/puntuacion",
            new { candidaturaId = candidatura.Id, puntos = 1 }), HttpStatusCode.Conflict);
        var proximoCierre = await worker.EjecutarCiclo(default);
        Assert.Equal(ronda.Cierre.AddDays(15), proximoCierre);
        Assert.Equal(proximoCierre, await worker.EjecutarCiclo(default));

        var resultados = await publico.GetFromJsonAsync<ResultadosRankingMusicaDto>($"{Ranking}/resultados?rondaId={ronda.Id}");
        Assert.NotNull(resultados);
        Assert.Equal(ronda.Id, resultados.Id);
        var decada = Assert.Single(resultados.Decadas);
        Assert.Equal(1980, decada.Decada);
        var ganadora = Assert.Single(decada.Ganadoras);
        Assert.Equal(candidatura.Id, ganadora.CandidaturaId);
        Assert.Equal("Canción corregida", ganadora.Titulo);
        Assert.Equal(5L, ganadora.Puntos);
        Assert.Empty(decada.Clasificadas);
        var siguiente = await publico.GetFromJsonAsync<RankingMusicaActualDto>($"{Ranking}/actual");
        Assert.Equal(ronda.Cierre, siguiente?.Apertura);
    }

    [Fact]
    public async Task QueSwaggerExpongaLasRutasAcordadasYSusRespuestas()
    {
        // Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var cliente = factory.CreateClient();

        // Act
        using var swagger = await cliente.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json");
        var rutas = swagger!.RootElement.GetProperty("paths");

        // Assert
        Assert.True(rutas.GetProperty($"{Ranking}/actual").TryGetProperty("get", out _));
        Assert.True(rutas.GetProperty($"{Ranking}/resultados").TryGetProperty("get", out _));
        Assert.True(rutas.GetProperty($"{Ranking}/sugerencias").TryGetProperty("post", out _));
        Assert.True(rutas.GetProperty(RankingAdmin).TryGetProperty("post", out _));
        Assert.True(rutas.GetProperty($"{Ranking}/{{id}}").TryGetProperty("put", out _));
        Assert.True(rutas.GetProperty($"{Ranking}/{{id}}").TryGetProperty("delete", out _));
        var voto = rutas.GetProperty($"{Ranking}/puntuacion").GetProperty("post");
        Assert.True(voto.TryGetProperty("requestBody", out _));
        Assert.True(voto.GetProperty("responses").TryGetProperty("429", out _));
    }

    private static async Task<CandidaturaRankingMusicaDto> CrearCandidatura(
        HttpClient cliente, string ruta, CrearCandidaturaMusicaDto propuesta)
    {
        using var respuesta = await cliente.PostAsJsonAsync(ruta, propuesta);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<CandidaturaRankingMusicaDto>())!;
    }

    private static async Task Autenticar(PublicacionesApiFactory factory, HttpClient cliente, string nombre,
        ReputacionEnum reputacion, bool esAdmin = false)
    {
        const string contraseña = "PruebaRanking123!";
        var email = $"{nombre}@example.test";
        using var registro = await cliente.PostAsJsonAsync("/api/auth/registro", new RegistroDto
        {
            Username = nombre,
            Email = email,
            Contraseña = contraseña
        });
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        var cuenta = (await registro.Content.ReadFromJsonAsync<AuthRespuestaDto>())!;
        await using var scope = factory.Services.CreateAsyncScope();
        var contexto = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await contexto.Usuario_Personales.Where(u => u.Id == cuenta.UsuarioId).ExecuteUpdateAsync(cambios => cambios
            .SetProperty(u => u.Reputacion, reputacion)
            .SetProperty(u => u.EsAdmin, esAdmin));

        if (esAdmin)
        {
            using var ingreso = await cliente.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Contraseña = contraseña });
            Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);
            cuenta = (await ingreso.Content.ReadFromJsonAsync<AuthRespuestaDto>())!;
        }
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cuenta.Token);
    }

    private static async Task VerificarEstado(HttpResponseMessage respuesta, HttpStatusCode esperado)
    {
        using (respuesta)
            Assert.True(esperado == respuesta.StatusCode, $"{respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
    }
}
