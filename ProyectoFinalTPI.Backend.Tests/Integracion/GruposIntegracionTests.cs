using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.Grupo;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class GruposIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    [Fact]
    public async Task QueExpulsarPorQueryQuiteSoloLaMembresiaYConserveLosRecuerdos()
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await PrepararGrupo(factory);
        await Autenticar(client, "creador@test.com");

        using var respuesta = await client.DeleteAsync("/api/grupos/4/integrantes?usuarioIdAEliminar=20");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Empty(await respuesta.Content.ReadAsByteArrayAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.GruposIntegrantes.AnyAsync(i => i.GrupoId == 4 && i.UsuarioId == 20));
        Assert.True(await db.GruposIntegrantes.AnyAsync(i => i.GrupoId == 4 && i.UsuarioId == 10));
        Assert.True(await db.Usuarios.AnyAsync(u => u.Id == 20));
        Assert.True(await db.Publicaciones.AnyAsync(p => p.Id == 1 && p.GrupoId == 4 && p.UsuarioId == 20));

        var integrantes = await client.GetFromJsonAsync<List<GrupoIntegranteDto>>("/api/grupos/4/integrantes");
        Assert.NotNull(integrantes);
        Assert.Single(integrantes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?usuarioIdAEliminar=")]
    [InlineData("?usuarioIdAEliminar=abc")]
    [InlineData("?usuarioIdAEliminar=0")]
    [InlineData("?usuarioIdAEliminar=-1")]
    [InlineData("?usuarioIdAEliminar=2147483648")]
    public async Task ExpulsarRechazaQueryAusenteOInvalidaSinQuitarIntegrantes(string query)
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await PrepararGrupo(factory);
        await Autenticar(client, "creador@test.com");

        using var respuesta = await client.DeleteAsync("/api/grupos/4/integrantes" + query);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        await VerificarMembresiasConservadas(factory);
    }

    [Theory]
    [InlineData(null, 4, 20, HttpStatusCode.Unauthorized)]
    [InlineData("integrante@test.com", 4, 10, HttpStatusCode.Forbidden)]
    [InlineData("creador@test.com", 4, 10, HttpStatusCode.BadRequest)]
    [InlineData("creador@test.com", 99, 20, HttpStatusCode.NotFound)]
    [InlineData("creador@test.com", 4, 99, HttpStatusCode.NotFound)]
    [InlineData("creador@test.com", 0, 20, HttpStatusCode.BadRequest)]
    public async Task ExpulsarRespetaAutenticacionPermisosYExistencia(
        string? email, int grupoId, int usuarioIdAEliminar, HttpStatusCode esperado)
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await PrepararGrupo(factory);
        if (email != null) await Autenticar(client, email);

        using var respuesta = await client.DeleteAsync(
            $"/api/grupos/{grupoId}/integrantes?usuarioIdAEliminar={usuarioIdAEliminar}");

        Assert.Equal(esperado, respuesta.StatusCode);
        await VerificarMembresiasConservadas(factory);
    }

    private static async Task VerificarMembresiasConservadas(PublicacionesApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.GruposIntegrantes.CountAsync(i => i.GrupoId == 4));
    }

    private static async Task Autenticar(HttpClient client, string email)
    {
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, contraseña = "clave-test" });
        login.EnsureSuccessStatusCode();
        var sesion = await login.Content.ReadFromJsonAsync<AuthRespuestaDto>();
        Assert.NotNull(sesion);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion.Token);
    }

    private static async Task PrepararGrupo(PublicacionesApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var password = BCrypt.Net.BCrypt.HashPassword("clave-test");
        var creador = new Usuario_Personal
        {
            Id = 10, Username = "creador", Email = "creador@test.com", Contraseña = password
        };
        var integrante = new Usuario_Personal
        {
            Id = 20, Username = "integrante", Email = "integrante@test.com", Contraseña = password
        };
        var grupo = new Grupo
        {
            Id = 4, Nombre = "Recuerdos", Decada = 1990, Creador = creador,
            Integrantes = new List<GrupoIntegrante>
            {
                new() { Usuario = creador }, new() { Usuario = integrante }
            }
        };
        db.Publicaciones.Add(new Publicacion
        {
            Id = 1, Titulo = "Recuerdo del integrante", Grupo = grupo, Usuario = integrante,
            Fecha = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Lugar = new Lugar
            {
                Nombre = "Buenos Aires", Direccion = "Buenos Aires",
                Coordenadas = new Point(-58.38, -34.60) { SRID = 4326 }
            }
        });
        await db.SaveChangesAsync();
    }
}
