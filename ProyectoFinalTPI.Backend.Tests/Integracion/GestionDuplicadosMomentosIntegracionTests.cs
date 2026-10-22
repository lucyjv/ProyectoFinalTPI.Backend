using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetTopologySuite.Geometries;
using Npgsql;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class GestionDuplicadosMomentosIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private const string Ruta = "/api/admin/momentos/1/fusion";
    private static async Task Preparar(PublicacionesApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Usuarios.AddRange(new Usuario_Personal { Id = 7, Username = "admin", Email = "admin@test.com", EsAdmin = true, Contraseña = BCrypt.Net.BCrypt.HashPassword("password123", 4) },
            new Usuario_Personal { Id = 8, Username = "autor", Email = "autor@test.com", Contraseña = BCrypt.Net.BCrypt.HashPassword("password123", 4) });
        db.Lugares.Add(new Lugar { Id = 1, Nombre = "Obelisco", Coordenadas = new Point(-58.3816, -34.6037) { SRID = 4326 } });
        db.Momentos.AddRange(new Momento { Id = 1, Nombre = "México 86", FechaInicio = new(1986, 1, 1), FechaFin = new(1986, 12, 31),
                Alias = [new() { Texto = "Argentina campeón 1986", TextoNormalizado = "argentina campeon 1986" }] },
            new Momento { Id = 2, Nombre = "Argentina campeón 1986", FechaInicio = new(1986, 1, 1), FechaFin = new(1986, 12, 31) },
            new Momento { Id = 3, Nombre = "El triunfo argentino 1986", FechaInicio = new(1986, 1, 1), FechaFin = new(1986, 12, 31) },
            new Momento { Id = 4, Nombre = "Argentina campeón 2022", FechaInicio = new(2022, 1, 1), FechaFin = new(2022, 12, 31) });
        db.Publicaciones.AddRange(Enumerable.Range(10, 2).Select(id => new Publicacion { Id = id, UsuarioId = 8, LugarId = 1, Titulo = "Los festejos del 86",
            Fecha = new DateTime(1986, 6, 29, 0, 0, 0, DateTimeKind.Utc) }));
        db.PublicacionesMomentos.AddRange(new() { PublicacionId = 10, MomentoId = 1, Origen = OrigenAsociacionMomento.Autor, Estado = EstadoAsociacionMomento.Rechazado },
            new() { PublicacionId = 10, MomentoId = 2, Origen = OrigenAsociacionMomento.Autor },
            new() { PublicacionId = 11, MomentoId = 1, Confianza = 0.95, VersionAnalisis = "test-v1" });
        await db.SaveChangesAsync();
        // La secuencia debe acompañar a los IDs explícitos del fixture.
        await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('momentos', 'Id'), (SELECT MAX(\"Id\") FROM momentos))");
    }

    private static async Task Autenticar(HttpClient cliente, string usuario = "admin")
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { email = $"{usuario}@test.com", contrasena = "password123" });
        respuesta.EnsureSuccessStatusCode();
        var sesion = await respuesta.Content.ReadFromJsonAsync<AuthRespuestaDto>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion!.Token);
    }

    [Fact]
    public async Task QueSoloAdministradoresPuedanBuscarPrevisualizarYFusionar()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var cliente = factory.CreateClient();
        await Preparar(factory);

        //Act
        using var anonimo = await cliente.GetAsync("/api/admin/momentos/1/duplicados");
        await Autenticar(cliente, "autor");
        using var buscar = await cliente.GetAsync("/api/admin/momentos/1/duplicados");
        using var ver = await cliente.GetAsync(Ruta + "?destinoId=2");
        using var fusionar = await cliente.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(2, 1, 1));

        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, buscar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ver.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, fusionar.StatusCode);
    }

    [Fact]
    public async Task QueLaVistaPreviaNoModifiqueDatosYLaFusionMantengaLosEnlacesAnteriores()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);
        var vista = await cliente.GetFromJsonAsync<VistaFusionMomentoDto>(Ruta + "?destinoId=2");

        //Act
        using var respuesta = await cliente.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(2, vista!.Origen.VersionCatalogo, vista.Destino.VersionCatalogo));
        var detalle = await cliente.GetFromJsonAsync<MomentoDetalleDto>("/api/momentos/1");
        using var publicaciones = await cliente.GetAsync("/api/momentos/1/publicaciones");
        using var repetida = await cliente.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(2, 1, 1));

        //Assert
        Assert.Equal(2, vista.PublicacionesAfectadas);
        Assert.Equal(1, vista.DecisionesEnConflicto);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(2, detalle!.Id);
        Assert.Equal(1, detalle.RedirigidoDesdeId);
        Assert.Contains("México 86", detalle.Alias);
        Assert.Equal(HttpStatusCode.OK, publicaciones.StatusCode);
        Assert.True((await repetida.Content.ReadFromJsonAsync<ResultadoFusionMomentoDto>())!.YaFusionado);
        using var consulta = factory.Services.CreateScope();
        var db = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4, await db.Momentos.CountAsync());
        Assert.Equal(2, await db.PublicacionesMomentos.CountAsync());
        Assert.All(await db.PublicacionesMomentos.ToListAsync(), a => Assert.Equal(2, a.MomentoId));
        Assert.Equal(EstadoAsociacionMomento.Rechazado, (await db.PublicacionesMomentos.SingleAsync(a => a.PublicacionId == 10)).Estado);
        Assert.All(await db.Publicaciones.ToListAsync(), p => Assert.Equal(1, p.VersionRelaciones));
    }

    [Fact]
    public async Task QueUnaCorreccionViejaFalleYUnaNuevaResuelvaIdsFusionadosSinDuplicarlos()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var admin = factory.CreateClient();
        using var autor = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(admin);
        await Autenticar(autor, "autor");
        (await admin.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(2, 1, 1))).EnsureSuccessStatusCode();

        //Act
        using var vieja = await autor.PutAsJsonAsync("/api/publicaciones/10/momentos", new { momentoIds = new[] { 1 }, permitirMomentosAutomaticos = true, versionRelaciones = 0 });
        using var nueva = await autor.PutAsJsonAsync("/api/publicaciones/10/momentos", new { momentoIds = new[] { 1, 2 }, permitirMomentosAutomaticos = true, versionRelaciones = 1 });

        //Assert
        Assert.Equal(HttpStatusCode.Conflict, vieja.StatusCode);
        Assert.Equal(HttpStatusCode.OK, nueva.StatusCode);
        Assert.Equal(2, Assert.Single((await nueva.Content.ReadFromJsonAsync<MomentosPublicacionDto>())!.Momentos).Momento.Id);
    }

    [Fact]
    public async Task QueUnAnalisisEnVueloResuelvaElIdFusionadoSinRestaurarElRechazoDelAutor()
    {
        //Arrange
        var iniciado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continuar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var factory = new PublicacionesApiFactory(postgres)
        {
            ConfigurarServiciosAdicionales = servicios =>
            {
                servicios.RemoveAll<IAnalizadorRecuerdo>();
                servicios.AddSingleton<IAnalizadorRecuerdo>(new AnalizadorEnVuelo(iniciado, continuar));
            }
        };
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);
        using var scope = factory.Services.CreateScope();
        var procesando = scope.ServiceProvider.GetRequiredService<IAnalisisRecuerdosServicio>().ProcesarUno(CancellationToken.None);
        await iniciado.Task.WaitAsync(TimeSpan.FromSeconds(10));

        //Act
        using var fusion = await cliente.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(2, 1, 1));
        continuar.SetResult();
        await procesando;

        //Assert
        Assert.Equal(HttpStatusCode.OK, fusion.StatusCode);
        using var consulta = factory.Services.CreateScope();
        var db = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var asociacion = await db.PublicacionesMomentos.SingleAsync(a => a.PublicacionId == 10);
        Assert.Equal(2, asociacion.MomentoId);
        Assert.Equal(OrigenAsociacionMomento.Autor, asociacion.Origen);
        Assert.Equal(EstadoAsociacionMomento.Rechazado, asociacion.Estado);
        Assert.Equal(EstadoTrabajoAnalisis.Completado, (await db.TrabajosAnalisisRecuerdos.SingleAsync(j => j.PublicacionId == 10)).Estado);
    }

    [Fact]
    public async Task QueDosFusionesConcurrentesDelMismoOrigenSoloConfirmenUnDestino()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);

        //Act
        var respuestas = await Task.WhenAll(cliente.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(2, 1, 1)),
            cliente.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(3, 1, 1)));

        //Assert
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
        using var consulta = factory.Services.CreateScope();
        var db = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Contains((await db.Momentos.FindAsync(1))!.MomentoCanonicoId, new int?[] { 2, 3 });
        foreach (var r in respuestas) r.Dispose();
    }

    [Fact]
    public async Task QueDosAltasConcurrentesConLaMismaIdentidadReutilicenUnSoloMomento()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();

        //Act
        var ids = await Task.WhenAll(scope1.ServiceProvider.GetRequiredService<IMomentoRepositorio>().Crear(new Momento { Nombre = "México-1970" }),
            scope2.ServiceProvider.GetRequiredService<IMomentoRepositorio>().Crear(new Momento { Nombre = "MEXICO 1970" }));

        //Assert
        Assert.Equal(ids[0], ids[1]);
        using var consulta = factory.Services.CreateScope();
        Assert.Equal(5, await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().Momentos.CountAsync());
    }

    [Fact]
    public async Task QueElIndiceUnicoImpidaDuplicadosAunqueSeOmitaElRepositorio()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Momentos.Add(new Momento { Nombre = "ARGENTINA CAMPEON 1986", FechaInicio = new(1986, 1, 1), FechaFin = new(1986, 12, 31) });

        //Act
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        //Assert
        Assert.Equal("23505", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task QueUnErrorDePersistenciaReviertaVinculosAliasVersionesYRedireccion()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fallar_fusion_test() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'fallo de fusión de prueba'; END; $$;
                CREATE TRIGGER fallar_fusion BEFORE INSERT OR UPDATE ON publicaciones_momentos
                FOR EACH ROW EXECUTE FUNCTION fallar_fusion_test();
                """);
        }

        //Act
        using var respuesta = await cliente.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(2, 1, 1));

        //Assert
        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        using var consulta = factory.Services.CreateScope();
        var dbConsulta = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null((await dbConsulta.Momentos.FindAsync(1))!.MomentoCanonicoId);
        Assert.Equal(3, await dbConsulta.PublicacionesMomentos.CountAsync());
        Assert.Equal(1, await dbConsulta.MomentosAlias.CountAsync());
        Assert.All(await dbConsulta.Publicaciones.ToListAsync(), p => Assert.Equal(0, p.VersionRelaciones));
    }

    [Fact]
    public async Task QueNoSeFusionenAniosIncompatiblesNiVersionesObsoletas()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        await Autenticar(cliente);

        //Act
        using var otroAnio = await cliente.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(4, 1, 1));
        using var vieja = await cliente.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(2, 1, 9));

        //Assert
        Assert.Equal(HttpStatusCode.Conflict, otroAnio.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, vieja.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueLosDuplicadosLegadosSinClaveSeDetectenAntesDeCrearOtroYSePuedanFusionar(bool origenIndexado)
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlRawAsync("UPDATE momentos SET \"Nombre\" = 'Argentina campeon 1986', \"ClaveIdentidad\" = NULL WHERE \"Id\" IN (1,2)");
            if (origenIndexado)
            {
                var origen = await db.Momentos.SingleAsync(m => m.Id == 1);
                origen.ClaveIdentidad = IdentidadMomento.Clave(origen.Nombre, origen.FechaInicio, origen.FechaFin);
                await db.SaveChangesAsync();
            }
        }
        await Autenticar(cliente);
        using var altaScope = factory.Services.CreateScope();
        var repo = altaScope.ServiceProvider.GetRequiredService<IMomentoRepositorio>();

        //Act
        var conflicto = await Assert.ThrowsAsync<ConflictoCatalogoException>(() => repo.Crear(new Momento
            { Nombre = "Argentina campeón 1986", FechaInicio = new(1986, 1, 1), FechaFin = new(1986, 12, 31) }));
        using var fusion = await cliente.PostAsJsonAsync(Ruta, new SolicitudFusionMomentoDto(2, origenIndexado ? 2 : 1, 1));

        //Assert
        Assert.Contains("duplicadas", conflicto.Message);
        Assert.Equal(HttpStatusCode.OK, fusion.StatusCode);
        using var consulta = factory.Services.CreateScope();
        var dbConsulta = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull((await dbConsulta.Momentos.FindAsync(2))!.ClaveIdentidad);
        Assert.Equal(4, await dbConsulta.Momentos.CountAsync());
    }

    private sealed class AnalizadorEnVuelo(TaskCompletionSource iniciado, TaskCompletionSource continuar) : IAnalizadorRecuerdo
    {
        public string Version => "fusion-en-vuelo-v1";
        public async Task<ResultadoAnalisisRecuerdoDto> Analizar(EntradaAnalisisRecuerdoDto entrada, CancellationToken ct)
        {
            iniciado.TrySetResult();
            await continuar.Task.WaitAsync(ct);
            return new([new(1, 0.95)]);
        }
    }
}
