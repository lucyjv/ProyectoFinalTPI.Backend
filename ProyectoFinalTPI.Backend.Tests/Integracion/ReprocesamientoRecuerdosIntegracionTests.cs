using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class ReprocesamientoRecuerdosIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private PublicacionesApiFactory CrearFactory(AnalizadorFake? fake = null) => new(postgres)
    {
        ConfigurarServiciosAdicionales = servicios =>
        {
            // Controlar el worker manualmente para comprobar el encolado antes del consumo.
            foreach (var registro in servicios.Where(d => d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(ProyectoFinalTPI.Backend.Services.AnalisisRecuerdosWorker)).ToArray()) servicios.Remove(registro);
            servicios.Configure<OpcionesAnalisisRecuerdos>(o => o.Habilitado = true);
            if (fake != null)
            {
                servicios.RemoveAll<IAnalizadorRecuerdo>();
                servicios.AddSingleton<IAnalizadorRecuerdo>(fake);
            }
        }
    };

    private static async Task Preparar(PublicacionesApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Usuarios.AddRange(new Usuario_Personal { Id = 7, Username = "admin", Email = "admin@test.com", EsAdmin = true,
                Contraseña = BCrypt.Net.BCrypt.HashPassword("password123", 4) },
            new Usuario_Personal { Id = 8, Username = "autor", Email = "autor@test.com", Contraseña = BCrypt.Net.BCrypt.HashPassword("password123", 4) });
        db.Lugares.Add(new Lugar { Id = 1, Nombre = "Obelisco", Coordenadas = new Point(-58.3816, -34.6037) { SRID = 4326 } });
        db.Momentos.Add(new Momento { Id = 1, Nombre = "Argentina campeón 1986" });
        db.Publicaciones.AddRange(Enumerable.Range(10, 3).Select(id => new Publicacion { Id = id, UsuarioId = 8, LugarId = 1,
            Titulo = "Argentina campeón 1986", Fecha = new DateTime(1986, 6, 29, 0, 0, 0, DateTimeKind.Utc) }));
        await db.SaveChangesAsync();
        var trabajos = await db.TrabajosAnalisisRecuerdos.ToListAsync();
        db.TrabajosAnalisisRecuerdos.Remove(trabajos.Single(j => j.PublicacionId == 10));
        trabajos.Single(j => j.PublicacionId == 11).Estado = EstadoTrabajoAnalisis.SinAnalizador;
        var fallido = trabajos.Single(j => j.PublicacionId == 12);
        fallido.Estado = EstadoTrabajoAnalisis.Fallido; fallido.Intentos = 3; fallido.CodigoError = "analisis_error";
        await db.SaveChangesAsync();
    }

    private static async Task Autenticar(HttpClient cliente, string usuario = "admin")
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { email = $"{usuario}@test.com", contrasena = "password123" });
        respuesta.EnsureSuccessStatusCode();
        var sesion = await respuesta.Content.ReadFromJsonAsync<AuthRespuestaDto>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion!.Token);
    }

    private const string Ruta = "/api/admin/analisis-recuerdos/reprocesamiento";

    [Fact]
    public async Task QueSoloUnAdministradorPuedaPrevisualizarYEncolarRecuerdos()
    {
        //Arrange
        await using var factory = CrearFactory(new());
        using var cliente = factory.CreateClient();
        await Preparar(factory);

        //Act
        using var anonimoGet = await cliente.GetAsync(Ruta);
        using var anonimoPost = await cliente.PostAsJsonAsync(Ruta, new SolicitudReprocesamientoDto([new(10, 1)]));
        await Autenticar(cliente, "autor");
        using var autorGet = await cliente.GetAsync(Ruta);
        using var autorPost = await cliente.PostAsJsonAsync(Ruta, new SolicitudReprocesamientoDto([new(10, 1)]));

        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, anonimoGet.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonimoPost.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, autorGet.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, autorPost.StatusCode);
        using var consulta = factory.Services.CreateScope();
        Assert.Equal(2, await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().TrabajosAnalisisRecuerdos.CountAsync());
    }

    [Fact]
    public async Task QueLaVistaPreviaSeaPaginadaYNoCambieLaColaNiLlameAlProveedor()
    {
        //Arrange
        var fake = new AnalizadorFake();
        await using var factory = CrearFactory(fake);
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);

        //Act
        var primera = await cliente.GetFromJsonAsync<VistaPreviaReprocesamientoDto>(Ruta + "?limite=1");
        var segunda = await cliente.GetFromJsonAsync<VistaPreviaReprocesamientoDto>(Ruta + $"?limite=1&despuesDeId={primera!.SiguienteDespuesDeId}");

        //Assert
        Assert.Equal(10, Assert.Single(primera.Recuerdos).PublicacionId);
        Assert.True(primera.AnalisisDisponible);
        Assert.Equal("sinAnalizador", Assert.Single(segunda!.Recuerdos).Estado);
        Assert.False(segunda.HayMas);
        Assert.Equal(0, fake.Llamadas);
        using var consulta = factory.Services.CreateScope();
        Assert.Equal(2, await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().TrabajosAnalisisRecuerdos.CountAsync());
    }

    [Fact]
    public async Task QueSeReprocesenRecuerdosExistentesSinRecargarlosYSeRespetenRechazosDelAutor()
    {
        //Arrange
        var fake = new AnalizadorFake();
        await using var factory = CrearFactory(fake);
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.PublicacionesMomentos.Add(new() { PublicacionId = 10, MomentoId = 1, Origen = OrigenAsociacionMomento.Autor, Estado = EstadoAsociacionMomento.Rechazado });
            await db.SaveChangesAsync();
        }

        //Act
        using var respuesta = await cliente.PostAsJsonAsync(Ruta, new SolicitudReprocesamientoDto([new(10, 1), new(11, 1)]));
        var lote = await respuesta.Content.ReadFromJsonAsync<ResultadoReprocesamientoDto>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var trabajos = await db.TrabajosAnalisisRecuerdos.Where(j => j.Estado == EstadoTrabajoAnalisis.Pendiente).ToListAsync();
            Assert.Equal(TimeSpan.FromSeconds(10), trabajos.Single(j => j.PublicacionId == 11).ProximoIntento - trabajos.Single(j => j.PublicacionId == 10).ProximoIntento);
            // Adelantar solo el reloj de elegibilidad del fixture, sin esperar en tiempo real.
            foreach (var trabajo in trabajos) trabajo.ProximoIntento = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        for (var i = 0; i < 2; i++)
        {
            using var scope = factory.Services.CreateScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<IAnalisisRecuerdosServicio>().ProcesarUno(CancellationToken.None));
        }

        //Assert
        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        Assert.Equal(2, lote!.Programados);
        Assert.Equal(2, fake.Llamadas);
        using var consulta = factory.Services.CreateScope();
        var dbConsulta = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(3, await dbConsulta.Publicaciones.CountAsync());
        Assert.Equal(3, await dbConsulta.TrabajosAnalisisRecuerdos.CountAsync());
        Assert.Equal(EstadoAsociacionMomento.Rechazado, (await dbConsulta.PublicacionesMomentos.SingleAsync(a => a.PublicacionId == 10)).Estado);
        Assert.Equal(OrigenAsociacionMomento.Automatico, (await dbConsulta.PublicacionesMomentos.SingleAsync(a => a.PublicacionId == 11)).Origen);
        Assert.Equal("completado", (await cliente.GetFromJsonAsync<EstadoAnalisisRecuerdoDto>("/api/publicaciones/11/analisis"))!.Estado);
    }

    [Fact]
    public async Task QueDosLotesConcurrentesNoDupliquenTrabajosNiReservasDeHorario()
    {
        //Arrange
        await using var factory = CrearFactory(new());
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();
        var repo1 = scope1.ServiceProvider.GetRequiredService<IReprocesamientoRecuerdosRepositorio>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<IReprocesamientoRecuerdosRepositorio>();
        var ahora = DateTime.UtcNow;

        //Act
        var lotes = await Task.WhenAll(repo1.Encolar(new([new(10, 1), new(11, 1)]), ahora, CancellationToken.None),
            repo2.Encolar(new([new(10, 1), new(12, 1)], IncluirFallidos: true), ahora, CancellationToken.None));

        //Assert
        Assert.Equal(3, lotes.Sum(l => l.Programados));
        Assert.Equal(1, lotes.Sum(l => l.Omitidos));
        using var consulta = factory.Services.CreateScope();
        var trabajos = await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().TrabajosAnalisisRecuerdos.OrderBy(j => j.ProximoIntento).ToArrayAsync();
        Assert.Equal(3, trabajos.Length);
        Assert.Equal(3, trabajos.Select(j => j.ProximoIntento).Distinct().Count());
        Assert.True(trabajos[1].ProximoIntento - trabajos[0].ProximoIntento >= TimeSpan.FromSeconds(10));
        Assert.True(trabajos[2].ProximoIntento - trabajos[1].ProximoIntento >= TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task QueSeOmitanLasVersionesEditadasYLasPreferenciasDesactivadasDespuesDePrevisualizar()
    {
        //Arrange
        await using var factory = CrearFactory(new());
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);
        var vista = await cliente.GetFromJsonAsync<VistaPreviaReprocesamientoDto>(Ruta);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Publicaciones.SingleAsync(p => p.Id == 10)).Titulo = "Texto editado";
            (await db.Publicaciones.SingleAsync(p => p.Id == 11)).PermitirMomentosAutomaticos = false;
            await db.SaveChangesAsync();
        }

        //Act
        using var respuesta = await cliente.PostAsJsonAsync(Ruta, new SolicitudReprocesamientoDto(
            vista!.Recuerdos.Select(r => new RecuerdoAReprocesarDto(r.PublicacionId, r.VersionContenido)).ToArray()));
        var result = await respuesta.Content.ReadFromJsonAsync<ResultadoReprocesamientoDto>();

        //Assert
        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        Assert.Equal(0, result!.Programados);
        Assert.Equal("versionDesactualizada", result.Recuerdos.Single(r => r.PublicacionId == 10).Motivo);
        Assert.Equal("automatizacionDesactivada", result.Recuerdos.Single(r => r.PublicacionId == 11).Motivo);
        using var consulta = factory.Services.CreateScope();
        var dbConsulta = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await dbConsulta.TrabajosAnalisisRecuerdos.AnyAsync(j => j.PublicacionId == 10 && j.VersionContenido == 1));
        Assert.Equal(EstadoTrabajoAnalisis.SinAnalizador, (await dbConsulta.TrabajosAnalisisRecuerdos.SingleAsync(j => j.PublicacionId == 11)).Estado);
    }

    [Fact]
    public async Task QueUnFalloAlEncolarReviertaElLoteCompleto()
    {
        //Arrange
        await using var factory = CrearFactory(new());
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fallar_reprogramacion_test() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN IF NEW."PublicacionId" = 11 THEN RAISE EXCEPTION 'fallo del lote de prueba'; END IF; RETURN NEW; END; $$;
                CREATE TRIGGER fallar_reprogramacion BEFORE UPDATE ON trabajos_analisis_recuerdos
                FOR EACH ROW EXECUTE FUNCTION fallar_reprogramacion_test();
                """);
        }

        //Act
        using var respuesta = await cliente.PostAsJsonAsync(Ruta, new SolicitudReprocesamientoDto([new(10, 1), new(11, 1)]));

        //Assert
        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        using var consulta = factory.Services.CreateScope();
        var trabajos = await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().TrabajosAnalisisRecuerdos.ToListAsync();
        Assert.DoesNotContain(trabajos, j => j.PublicacionId == 10);
        Assert.Equal(EstadoTrabajoAnalisis.SinAnalizador, trabajos.Single(j => j.PublicacionId == 11).Estado);
    }

    [Fact]
    public async Task QueUnProveedorDeshabilitadoPermitaVerLaSeleccionPeroNoEncolarla()
    {
        //Arrange
        await using var factory = CrearFactory();
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);

        //Act
        var vista = await cliente.GetFromJsonAsync<VistaPreviaReprocesamientoDto>(Ruta);
        using var respuesta = await cliente.PostAsJsonAsync(Ruta, new SolicitudReprocesamientoDto([new(10, 1)]));
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();

        //Assert
        Assert.False(vista!.AnalisisDisponible);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("Análisis no disponible", problema!.Title);
        using var consulta = factory.Services.CreateScope();
        Assert.Equal(2, await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().TrabajosAnalisisRecuerdos.CountAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"recuerdos\":[]}")]
    [InlineData("{\"recuerdos\":[{\"publicacionId\":10}]}")]
    [InlineData("{\"recuerdos\":[{\"publicacionId\":10,\"versionContenido\":1}],\"intervaloSegundos\":1}")]
    public async Task QueUnLoteInvalidoDevuelvaBadRequestSinCambiarLaCola(string json)
    {
        //Arrange
        await using var factory = CrearFactory(new());
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);
        using var contenido = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        //Act
        using var respuesta = await cliente.PostAsync(Ruta, contenido);

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var consulta = factory.Services.CreateScope();
        Assert.Equal(2, await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().TrabajosAnalisisRecuerdos.CountAsync());
    }

    private sealed class AnalizadorFake : IAnalizadorRecuerdo
    {
        public string Version => "reprocesamiento-test-v1";
        public int Llamadas;
        public Task<ResultadoAnalisisRecuerdoDto> Analizar(EntradaAnalisisRecuerdoDto entrada, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(new ResultadoAnalisisRecuerdoDto([new(1, 0.95)]));
        }
    }
}
