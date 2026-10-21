using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class ExploracionMomentosIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private static readonly DateTime Fecha = new(1986, 6, 29, 0, 0, 0, DateTimeKind.Utc);

    private static async Task Preparar(PublicacionesApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Usuarios.AddRange(
            new Usuario_Personal { Id = 7, Username = "autor", Email = "autor@test.com", Contraseña = BCrypt.Net.BCrypt.HashPassword("password123", 4) },
            new Usuario_Personal { Id = 8, Username = "otro", Email = "otro@test.com", Contraseña = BCrypt.Net.BCrypt.HashPassword("password123", 4) });
        db.Lugares.Add(new Lugar { Id = 1, Nombre = "Obelisco", Coordenadas = new Point(-58.3816, -34.6037) { SRID = 4326 } });
        db.Publicaciones.Add(new Publicacion { Id = 10, UsuarioId = 7, LugarId = 1, Titulo = "Los festejos del 86", Fecha = Fecha });
        db.Momentos.AddRange(new Momento { Id = 1, Nombre = "Mundial de México 1986" },
            new Momento { Id = 2, Nombre = "Argentina campeón 1986", MomentoPadreId = 1 },
            new Momento { Id = 3, Nombre = "La final", MomentoPadreId = 2 },
            new Momento { Id = 4, Nombre = "Argentina campeón 2022" });
        db.MomentosAlias.Add(new MomentoAlias { MomentoId = 1, Texto = "México 86", TextoNormalizado = "mexico 86" });
        await db.SaveChangesAsync();
    }

    private static async Task Autenticar(HttpClient client, string usuario = "autor")
    {
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = usuario + "@test.com", contrasena = "password123" });
        login.EnsureSuccessStatusCode();
        var respuesta = await login.Content.ReadFromJsonAsync<AuthRespuestaDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", respuesta!.Token);
    }

    [Fact]
    public async Task QueSePuedanBuscarMomentosPorAliasYRecorrerSuJerarquiaSinSesion()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);

        //Act
        var porAlias = await client.GetFromJsonAsync<ResultadoPaginadoDto<MomentoResumenDto>>("/api/momentos?busqueda=Mexico%2086");
        var primera = await client.GetFromJsonAsync<ResultadoPaginadoDto<MomentoResumenDto>>("/api/momentos?limite=1");
        var segunda = await client.GetFromJsonAsync<ResultadoPaginadoDto<MomentoResumenDto>>(
            "/api/momentos?limite=1&cursor=" + Uri.EscapeDataString(primera!.SiguienteCursor!));
        var detalle = await client.GetFromJsonAsync<MomentoDetalleDto>("/api/momentos/2");

        //Assert
        Assert.Equal(1, Assert.Single(porAlias!.Items).Id);
        Assert.Equal(1, Assert.Single(primera.Items).Id);
        Assert.Equal(2, Assert.Single(segunda!.Items).Id);
        Assert.Equal(1, detalle!.Padre!.Id);
        Assert.Equal(3, Assert.Single(detalle.Hijos).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/momentos?busqueda=otro&cursor=" + Uri.EscapeDataString(primera.SiguienteCursor!))).StatusCode);
    }

    [Fact]
    public async Task QueSePaginenDescendientesSinDuplicadosOcultosRepostsNiRechazos()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Publicaciones.AddRange(Enumerable.Range(11, 4).Select(id => new Publicacion
            {
                Id = id, UsuarioId = 7, LugarId = 1, Fecha = Fecha, Titulo = "Historia " + id,
                EstaOculto = id == 12, PublicacionOriginalId = id == 13 ? 10 : null
            }));
            db.PublicacionesMomentos.AddRange(new PublicacionMomento { PublicacionId = 10, MomentoId = 2 },
                new PublicacionMomento { PublicacionId = 10, MomentoId = 3 },
                new PublicacionMomento { PublicacionId = 11, MomentoId = 1 },
                new PublicacionMomento { PublicacionId = 12, MomentoId = 1 },
                new PublicacionMomento { PublicacionId = 13, MomentoId = 1 },
                new PublicacionMomento { PublicacionId = 14, MomentoId = 1, Estado = EstadoAsociacionMomento.Rechazado });
            await db.SaveChangesAsync();
        }

        //Act
        var primera = await client.GetFromJsonAsync<ResultadoPaginadoDto<PublicacionResumenDto>>("/api/momentos/1/publicaciones?limite=1");
        var segunda = await client.GetFromJsonAsync<ResultadoPaginadoDto<PublicacionResumenDto>>(
            "/api/momentos/1/publicaciones?limite=1&cursor=" + Uri.EscapeDataString(primera!.SiguienteCursor!));
        var directos = await client.GetFromJsonAsync<ResultadoPaginadoDto<PublicacionResumenDto>>("/api/momentos/1/publicaciones?incluirDescendientes=false");

        //Assert
        Assert.Equal(11, Assert.Single(primera.Items).Id);
        Assert.Equal(10, Assert.Single(segunda!.Items).Id);
        Assert.False(segunda.HayMas);
        Assert.Equal(11, Assert.Single(directos!.Items).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(
            "/api/momentos/1/publicaciones?incluirDescendientes=false&cursor=" + Uri.EscapeDataString(primera.SiguienteCursor!))).StatusCode);
    }

    [Fact]
    public async Task QueElAutorCorrijaLaSeleccionYElAnalisisRespeteSusRechazosYPreferencias()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IMomentoServicio>().AsociarAutomaticamente(10, 1, 0.9, "v1");
        await Autenticar(client);
        var inicial = await client.GetFromJsonAsync<MomentosPublicacionDto>("/api/publicaciones/10/momentos");

        //Act
        using var respuesta = await client.PutAsJsonAsync("/api/publicaciones/10/momentos", new
        {
            momentoIds = new[] { 2 }, permitirMomentosAutomaticos = false, versionRelaciones = inicial!.VersionRelaciones
        });
        var corregido = await respuesta.Content.ReadFromJsonAsync<MomentosPublicacionDto>();
        bool autoPermitido;
        using (var scope = factory.Services.CreateScope())
            autoPermitido = await scope.ServiceProvider.GetRequiredService<IMomentoServicio>().AsociarAutomaticamente(10, 4, 0.99, "v2");
        using var habilitar = await client.PutAsJsonAsync("/api/publicaciones/10/momentos", new
        {
            momentoIds = new[] { 2 }, permitirMomentosAutomaticos = true, versionRelaciones = corregido!.VersionRelaciones
        });
        bool rechazoRespetado;
        using (var scope = factory.Services.CreateScope())
            rechazoRespetado = !await scope.ServiceProvider.GetRequiredService<IMomentoServicio>().AsociarAutomaticamente(10, 1, 0.99, "v3");

        //Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(inicial!.PuedeCorregir);
        Assert.Equal("automatico", Assert.Single(inicial.Momentos).Origen);
        Assert.Equal(inicial.VersionRelaciones + 1, corregido.VersionRelaciones);
        Assert.Equal(2, Assert.Single(corregido.Momentos).Momento.Id);
        Assert.Equal("autor", corregido.Momentos[0].Origen);
        Assert.False(corregido.PermitirMomentosAutomaticos);
        Assert.False(autoPermitido);
        Assert.Equal(HttpStatusCode.OK, habilitar.StatusCode);
        Assert.True(rechazoRespetado);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task QueNoSeCorrijaSinSesionODandoUnAutorAjenoEnElBody(bool autenticarOtro, HttpStatusCode codigo)
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        if (autenticarOtro) await Autenticar(client, "otro");

        //Act
        using var respuesta = await client.PutAsJsonAsync("/api/publicaciones/10/momentos", new
        {
            momentoIds = new[] { 1 }, permitirMomentosAutomaticos = false, versionRelaciones = 0, usuarioId = 7
        });

        //Assert
        Assert.Equal(codigo, respuesta.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PublicacionesMomentos.ToListAsync());
    }

    [Fact]
    public async Task QueSePuedaIndicarNingunMomentoYReactivarDespuesUnoRechazado()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(client);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IMomentoServicio>().AsociarAutomaticamente(10, 1, 0.9, "v1");

        //Act
        using var quitar = await client.PutAsJsonAsync("/api/publicaciones/10/momentos", new { momentoIds = Array.Empty<int>(), permitirMomentosAutomaticos = false, versionRelaciones = 1 });
        var vacio = await quitar.Content.ReadFromJsonAsync<MomentosPublicacionDto>();
        using var repetir = await client.PutAsJsonAsync("/api/publicaciones/10/momentos", new { momentoIds = Array.Empty<int>(), permitirMomentosAutomaticos = false, versionRelaciones = vacio!.VersionRelaciones });
        var igual = await repetir.Content.ReadFromJsonAsync<MomentosPublicacionDto>();
        using var reactivar = await client.PutAsJsonAsync("/api/publicaciones/10/momentos", new { momentoIds = new[] { 1 }, permitirMomentosAutomaticos = false, versionRelaciones = igual!.VersionRelaciones });
        var activo = await reactivar.Content.ReadFromJsonAsync<MomentosPublicacionDto>();

        //Assert
        Assert.Equal(HttpStatusCode.OK, quitar.StatusCode);
        Assert.Empty(vacio.Momentos);
        Assert.False(vacio.PermitirMomentosAutomaticos);
        Assert.Equal(vacio.VersionRelaciones, igual.VersionRelaciones);
        Assert.Equal(igual.VersionRelaciones + 1, activo!.VersionRelaciones);
        Assert.Equal(1, Assert.Single(activo.Momentos).Momento.Id);
        Assert.Equal("autor", activo.Momentos[0].Origen);
        using var consulta = factory.Services.CreateScope();
        Assert.Single(await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().PublicacionesMomentos.ToListAsync());
    }

    [Fact]
    public async Task QueNoSeApliqueUnaCorreccionParcialConMomentosInexistentes()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(client);

        //Act
        using var respuesta = await client.PutAsJsonAsync("/api/publicaciones/10/momentos", new { momentoIds = new[] { 1, 99 }, permitirMomentosAutomaticos = false, versionRelaciones = 0 });
        var estado = await client.GetFromJsonAsync<MomentosPublicacionDto>("/api/publicaciones/10/momentos");

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Empty(estado!.Momentos);
        Assert.Equal(0, estado.VersionRelaciones);
        Assert.True(estado.PermitirMomentosAutomaticos);
    }

    [Fact]
    public async Task QueUnAnalisisPosteriorALaLecturaDelEditorGenereConflictoAlGuardar()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(client);
        var leido = await client.GetFromJsonAsync<MomentosPublicacionDto>("/api/publicaciones/10/momentos");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IMomentoServicio>().AsociarAutomaticamente(10, 1, 0.9, "v1");

        //Act
        using var respuesta = await client.PutAsJsonAsync("/api/publicaciones/10/momentos", new { momentoIds = Array.Empty<int>(), permitirMomentosAutomaticos = false, versionRelaciones = leido!.VersionRelaciones });
        var actual = await client.GetFromJsonAsync<MomentosPublicacionDto>("/api/publicaciones/10/momentos");

        //Assert
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(1, Assert.Single(actual!.Momentos).Momento.Id);
        Assert.Equal("automatico", actual.Momentos[0].Origen);
        Assert.True(actual.PermitirMomentosAutomaticos);
    }

    [Fact]
    public async Task QueDosCorreccionesConcurrentesConLaMismaVersionNoSeSobrescriban()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(client);

        //Act
        var respuestas = await Task.WhenAll(
            client.PutAsJsonAsync("/api/publicaciones/10/momentos", new { momentoIds = new[] { 1 }, permitirMomentosAutomaticos = false, versionRelaciones = 0 }),
            client.PutAsJsonAsync("/api/publicaciones/10/momentos", new { momentoIds = new[] { 2 }, permitirMomentosAutomaticos = true, versionRelaciones = 0 }));
        var estado = await client.GetFromJsonAsync<MomentosPublicacionDto>("/api/publicaciones/10/momentos");

        //Assert
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        var conflicto = Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
        var problema = await conflicto.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(409, problema!.Status);
        Assert.Equal(1, estado!.VersionRelaciones);
        var id = Assert.Single(estado.Momentos).Momento.Id;
        Assert.Equal(id == 2, estado.PermitirMomentosAutomaticos);
        foreach (var respuesta in respuestas) respuesta.Dispose();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"momentoIds\":[],\"permitirMomentosAutomaticos\":false}")]
    [InlineData("{\"momentoIds\":[1,1],\"permitirMomentosAutomaticos\":true,\"versionRelaciones\":0}")]
    [InlineData("{\"momentoIds\":[0],\"permitirMomentosAutomaticos\":true,\"versionRelaciones\":0}")]
    public async Task QueLasCorreccionesInvalidasNoCambienLasAsociaciones(string json)
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(client);

        //Act
        using var respuesta = await client.PutAsync("/api/publicaciones/10/momentos", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Publicaciones.SingleAsync()).VersionRelaciones);
    }

    [Fact]
    public async Task QueLasLecturasResuelvanRepostsYNoExponganRecuerdosOcultos()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Publicaciones.Add(new Publicacion { Id = 11, UsuarioId = 7, LugarId = 1, Fecha = Fecha, PublicacionOriginalId = 10 });
            await db.SaveChangesAsync();
        }

        //Act
        var original = await client.GetFromJsonAsync<MomentosPublicacionDto>("/api/publicaciones/11/momentos");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Publicaciones.Where(p => p.Id == 10).ExecuteUpdateAsync(s => s.SetProperty(p => p.EstaOculto, true));
        using var oculto = await client.GetAsync("/api/publicaciones/11/momentos");

        //Assert
        Assert.Equal(10, original!.PublicacionId);
        Assert.False(original.PuedeCorregir);
        Assert.Empty(original.Momentos);
        Assert.Equal(HttpStatusCode.NotFound, oculto.StatusCode);
    }
}
