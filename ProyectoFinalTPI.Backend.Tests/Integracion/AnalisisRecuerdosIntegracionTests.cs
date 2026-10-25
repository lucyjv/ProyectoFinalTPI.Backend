using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Services;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class AnalisisRecuerdosIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private static async Task Preparar(PublicacionesApiFactory factory, bool crearRecuerdo = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Usuarios.Add(new Usuario_Personal { Id = 7, Username = "autor", Email = "cola@test.com" });
        db.Momentos.Add(new Momento { Id = 1, Nombre = "Mundial 1986" });
        if (crearRecuerdo)
        {
            db.Lugares.Add(new Lugar { Id = 1, Nombre = "Obelisco", Coordenadas = new Point(-58.3816, -34.6037) { SRID = 4326 } });
            db.Publicaciones.Add(Recuerdo(10));
        }
        await db.SaveChangesAsync();
    }

    private static Publicacion Recuerdo(int id) => new()
    {
        Id = id, UsuarioId = 7, LugarId = 1, Titulo = "Los festejos del 86", Descripcion = "Una historia en el Obelisco",
        Fecha = new DateTime(1986, 6, 29, 0, 0, 0, DateTimeKind.Utc)
    };

    private static MultipartFormDataContent Formulario()
    {
        var form = new MultipartFormDataContent();
        foreach (var dato in new Dictionary<string, string>
        {
            ["Titulo"] = "Recuerdo", ["Descripcion"] = "Una historia", ["Fecha"] = "1986-06-29T00:00:00Z",
            ["Categoria"] = "0", ["NombreLugar"] = "Obelisco", ["Latitud"] = "-34.6037", ["Longitud"] = "-58.3816",
            ["TipoMultimedia"] = "0", ["UrlArchivo"] = "https://example.com/foto.jpg", ["AutorId"] = "7"
        }) form.Add(new StringContent(dato.Value), dato.Key);
        return form;
    }

    private PublicacionesApiFactory CrearFactory(AnalizadorFake fake) => new(postgres)
    {
        ConfigurarServiciosAdicionales = servicios =>
        {
            servicios.RemoveAll<IAnalizadorRecuerdo>();
            servicios.AddSingleton<IAnalizadorRecuerdo>(fake);
        }
    };

    private static async Task<bool> Procesar(PublicacionesApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAnalisisRecuerdosServicio>().ProcesarUno(CancellationToken.None);
    }

    [Fact]
    public async Task QueCrearUnaPublicacionDevuelvaCreatedConTrabajoPendienteSinInvocarAlAnalizador()
    {
        //Arrange
        var analizador = new AnalizadorFake { Operacion = (_, _) => throw new InvalidOperationException("No debe analizar al publicar") };
        await using var factory = CrearFactory(analizador);
        using var client = factory.CreateClient();
        await Preparar(factory, crearRecuerdo: false);
        using var form = Formulario();

        //Act
        using var respuesta = await client.PostAsync("/api/publicaciones", form);

        //Assert
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal(0, analizador.Llamadas);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publicacion = await db.Publicaciones.SingleAsync();
        var trabajo = await db.TrabajosAnalisisRecuerdos.SingleAsync();
        Assert.Equal(publicacion.Id, trabajo.PublicacionId);
        Assert.Equal(1, trabajo.VersionContenido);
        Assert.Equal(EstadoTrabajoAnalisis.Pendiente, trabajo.Estado);
    }

    [Fact]
    public async Task QueUnFalloAlInsertarElTrabajoNoDejeLaPublicacionGuardadaSinCola()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory, crearRecuerdo: false);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Solo en esta base temporal: forzar un error al insertar el trabajo, después de insertar su recuerdo.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fallar_trabajo_test() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'fallo de prueba al encolar'; END; $$;
                CREATE TRIGGER fallar_trabajo BEFORE INSERT ON trabajos_analisis_recuerdos
                FOR EACH ROW EXECUTE FUNCTION fallar_trabajo_test();
                """);
        }
        using var form = Formulario();

        //Act
        using var respuesta = await client.PostAsync("/api/publicaciones", form);

        //Assert
        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        using var consulta = factory.Services.CreateScope();
        var dbConsulta = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await dbConsulta.Publicaciones.ToListAsync());
        Assert.Empty(await dbConsulta.TrabajosAnalisisRecuerdos.ToListAsync());
    }

    [Fact]
    public async Task QueElWorkerUseLaColaRealYExpongaSinAnalizadorSinInventarMomentos()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        var pendiente = await client.GetFromJsonAsync<EstadoAnalisisRecuerdoDto>("/api/publicaciones/10/analisis");
        var worker = factory.Services.GetServices<IHostedService>().OfType<AnalisisRecuerdosWorker>().Single();

        //Act
        var procesado = await worker.EjecutarCiclo(CancellationToken.None);
        var terminado = await client.GetFromJsonAsync<EstadoAnalisisRecuerdoDto>("/api/publicaciones/10/analisis");

        //Assert
        Assert.Equal("pendiente", pendiente!.Estado);
        Assert.True(procesado);
        Assert.Equal("sinAnalizador", terminado!.Estado);
        using var consulta = factory.Services.CreateScope();
        Assert.Empty(await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().PublicacionesMomentos.ToListAsync());
    }

    [Fact]
    public async Task QueDosWorkersNoPuedanReservarElMismoTrabajo()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        async Task<ReservaAnalisisDto?> Tomar()
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ITrabajoAnalisisRepositorio>().Reservar(DateTime.UtcNow, TimeSpan.FromMinutes(2), 3, CancellationToken.None);
        }

        //Act
        var reservas = await Task.WhenAll(Tomar(), Tomar());

        //Assert
        Assert.Single(reservas, r => r != null);
        Assert.Single(reservas, r => r == null);
    }

    [Fact]
    public async Task QueUnaFilaBloqueadaNoImpidaReservarElSiguienteTrabajo()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        using var bloqueo = factory.Services.CreateScope();
        var db = bloqueo.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Publicaciones.Add(Recuerdo(11));
        await db.SaveChangesAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.TrabajosAnalisisRecuerdos.FromSqlRaw("SELECT * FROM trabajos_analisis_recuerdos WHERE \"PublicacionId\" = 10 FOR UPDATE").ToListAsync();
        using var segundo = factory.Services.CreateScope();

        //Act
        var result = await segundo.ServiceProvider.GetRequiredService<ITrabajoAnalisisRepositorio>()
            .Reservar(DateTime.UtcNow, TimeSpan.FromMinutes(2), 3, CancellationToken.None);

        //Assert
        Assert.Equal(11, result!.PublicacionId);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task QueSeRecupereUnTrabajoInterrumpidoSinAceptarElResultadoDelTokenAnterior()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        ReservaAnalisisDto anterior;
        using (var scope = factory.Services.CreateScope())
        {
            anterior = (await scope.ServiceProvider.GetRequiredService<ITrabajoAnalisisRepositorio>().Reservar(DateTime.UtcNow, TimeSpan.FromMinutes(2), 3, CancellationToken.None))!;
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TrabajosAnalisisRecuerdos.ExecuteUpdateAsync(
                s => s.SetProperty(j => j.ReservadoHasta, DateTime.UtcNow.AddSeconds(-1)));
        }
        using var recuperacion = factory.Services.CreateScope();
        var repositorio = recuperacion.ServiceProvider.GetRequiredService<ITrabajoAnalisisRepositorio>();

        //Act
        var actual = await repositorio.Reservar(DateTime.UtcNow, TimeSpan.FromMinutes(2), 3, CancellationToken.None);
        var aceptado = await repositorio.Completar(anterior, new(new[] { new MomentoDetectadoDto(1, 0.9) }), "v1", DateTime.UtcNow, CancellationToken.None);

        //Assert
        Assert.Equal(anterior.Id, actual!.Id);
        Assert.NotEqual(anterior.TokenReserva, actual.TokenReserva);
        Assert.Equal(2, actual.Intentos);
        Assert.False(aceptado);
        Assert.Empty(await recuperacion.ServiceProvider.GetRequiredService<ApplicationDbContext>().PublicacionesMomentos.ToListAsync());
    }

    [Fact]
    public async Task QueNoSeGuardeUnResultadoDeUnaVersionAnteriorDelTexto()
    {
        //Arrange
        var analizador = new AnalizadorFake();
        await using var factory = CrearFactory(analizador);
        using var client = factory.CreateClient();
        await Preparar(factory);
        analizador.Operacion = async (_, _) =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var p = await db.Publicaciones.SingleAsync();
            p.Descripcion = "Texto editado durante el análisis";
            await db.SaveChangesAsync();
            return new(new[] { new MomentoDetectadoDto(1, 0.99) });
        };

        //Act
        await Procesar(factory);

        //Assert
        using var consulta = factory.Services.CreateScope();
        var dbConsulta = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await dbConsulta.PublicacionesMomentos.ToListAsync());
        Assert.Equal(2, (await dbConsulta.Publicaciones.SingleAsync()).VersionContenido);
        var trabajos = await dbConsulta.TrabajosAnalisisRecuerdos.OrderBy(j => j.VersionContenido).ToListAsync();
        Assert.Equal(EstadoTrabajoAnalisis.Descartado, trabajos[0].Estado);
        Assert.Equal(EstadoTrabajoAnalisis.Pendiente, trabajos[1].Estado);
    }

    [Fact]
    public async Task QueUnaCorreccionDuranteElAnalisisNoSeaSobrescrita()
    {
        //Arrange
        var analizador = new AnalizadorFake();
        await using var factory = CrearFactory(analizador);
        using var client = factory.CreateClient();
        await Preparar(factory);
        analizador.Operacion = async (_, _) =>
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IExploracionMomentosServicio>().Corregir(10, 7, new[] { 1 }, false, 0, CancellationToken.None);
            return new(new[] { new MomentoDetectadoDto(1, 0.99) });
        };

        //Act
        await Procesar(factory);

        //Assert
        using var consulta = factory.Services.CreateScope();
        var db = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(OrigenAsociacionMomento.Autor, (await db.PublicacionesMomentos.SingleAsync()).Origen);
        Assert.Equal(EstadoTrabajoAnalisis.Descartado, (await db.TrabajosAnalisisRecuerdos.SingleAsync()).Estado);
        Assert.False((await db.Publicaciones.SingleAsync()).PermitirMomentosAutomaticos);
    }

    [Fact]
    public async Task QueLosFallosSeReintentenHastaElLimiteYQuedenRegistradosSinTextoPrivado()
    {
        //Arrange
        var analizador = new AnalizadorFake { Operacion = (_, _) => throw new InvalidOperationException("Texto privado") };
        await using var factory = CrearFactory(analizador);
        using var client = factory.CreateClient();
        await Preparar(factory);

        //Act
        for (var intento = 0; intento < 3; intento++)
        {
            await Procesar(factory);
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TrabajosAnalisisRecuerdos
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.ProximoIntento, DateTime.UtcNow.AddSeconds(-1)));
        }
        var hayTrabajo = await Procesar(factory);

        //Assert
        Assert.False(hayTrabajo);
        Assert.Equal(3, analizador.Llamadas);
        using var consulta = factory.Services.CreateScope();
        var trabajo = await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().TrabajosAnalisisRecuerdos.SingleAsync();
        Assert.Equal(EstadoTrabajoAnalisis.Fallido, trabajo.Estado);
        Assert.Equal("analisis_error", trabajo.CodigoError);
        Assert.Null(trabajo.TokenReserva);
    }

    [Fact]
    public async Task QueUnFalloAlGuardarAsociacionesNoConfirmeParcialmenteElResultado()
    {
        //Arrange
        var analizador = new AnalizadorFake();
        await using var factory = CrearFactory(analizador);
        using var client = factory.CreateClient();
        await Preparar(factory);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fallar_asociacion_test() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'fallo de prueba al guardar resultado'; END; $$;
                CREATE TRIGGER fallar_asociacion BEFORE INSERT ON publicaciones_momentos
                FOR EACH ROW EXECUTE FUNCTION fallar_asociacion_test();
                """);

        //Act
        await Procesar(factory);

        //Assert
        using var consulta = factory.Services.CreateScope();
        var db = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.PublicacionesMomentos.ToListAsync());
        Assert.Equal(0, (await db.Publicaciones.SingleAsync()).VersionRelaciones);
        var trabajo = await db.TrabajosAnalisisRecuerdos.SingleAsync();
        Assert.Equal(EstadoTrabajoAnalisis.Pendiente, trabajo.Estado);
        Assert.Equal("analisis_error", trabajo.CodigoError);
    }

    [Fact]
    public async Task QueUnRecuerdoEliminadoDuranteElAnalisisNoDejeUnTrabajoHuerfano()
    {
        //Arrange
        var analizador = new AnalizadorFake();
        await using var factory = CrearFactory(analizador);
        using var client = factory.CreateClient();
        await Preparar(factory);
        analizador.Operacion = async (_, _) =>
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Publicaciones.ExecuteDeleteAsync();
            return new(new[] { new MomentoDetectadoDto(1, 0.9) });
        };

        //Act
        await Procesar(factory);

        //Assert
        using var consulta = factory.Services.CreateScope();
        var db = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.TrabajosAnalisisRecuerdos.ToListAsync());
        Assert.Empty(await db.PublicacionesMomentos.ToListAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/publicaciones/10/analisis")).StatusCode);
    }

    [Fact]
    public async Task QueUnMomentoInexistenteEnElResultadoNoDejeAsociacionesParciales()
    {
        //Arrange
        var analizador = new AnalizadorFake
        {
            Operacion = (_, _) => Task.FromResult(new ResultadoAnalisisRecuerdoDto(new[] { new MomentoDetectadoDto(1, 0.9), new MomentoDetectadoDto(99, 0.9) }))
        };
        await using var factory = CrearFactory(analizador);
        using var client = factory.CreateClient();
        await Preparar(factory);

        //Act
        await Procesar(factory);

        //Assert
        using var consulta = factory.Services.CreateScope();
        var db = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.PublicacionesMomentos.ToListAsync());
        Assert.Equal(EstadoTrabajoAnalisis.Fallido, (await db.TrabajosAnalisisRecuerdos.SingleAsync()).Estado);
    }

    [Fact]
    public async Task QueLaBaseImpidaDuplicarUnTrabajoParaLaMismaVersion()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        await Preparar(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.TrabajosAnalisisRecuerdos.Add(new TrabajoAnalisisRecuerdo { PublicacionId = 10, VersionContenido = 1 });

        //Act
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        //Assert
        Assert.Equal("23505", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task QueUnResultadoValidoSeConfirmeConSusAsociacionesYUnaNuevaVersionDeRelaciones()
    {
        //Arrange
        var analizador = new AnalizadorFake();
        await using var factory = CrearFactory(analizador);
        using var client = factory.CreateClient();
        await Preparar(factory);

        //Act
        await Procesar(factory);
        var repetido = await Procesar(factory);

        //Assert
        Assert.False(repetido);
        using var consulta = factory.Services.CreateScope();
        var db = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, (await db.PublicacionesMomentos.SingleAsync()).MomentoId);
        Assert.Equal(1, (await db.Publicaciones.SingleAsync()).VersionRelaciones);
        Assert.Equal(EstadoTrabajoAnalisis.Completado, (await db.TrabajosAnalisisRecuerdos.SingleAsync()).Estado);
        Assert.Equal(1, analizador.Llamadas);
    }

    private sealed class AnalizadorFake : IAnalizadorRecuerdo
    {
        public string Version => "integracion-v1";
        public int Llamadas;
        public Func<EntradaAnalisisRecuerdoDto, CancellationToken, Task<ResultadoAnalisisRecuerdoDto>>? Operacion;
        public Task<ResultadoAnalisisRecuerdoDto> Analizar(EntradaAnalisisRecuerdoDto entrada, CancellationToken ct)
        {
            Llamadas++;
            return Operacion?.Invoke(entrada, ct) ?? Task.FromResult(new ResultadoAnalisisRecuerdoDto(new[] { new MomentoDetectadoDto(1, 0.9) }));
        }
    }
}
