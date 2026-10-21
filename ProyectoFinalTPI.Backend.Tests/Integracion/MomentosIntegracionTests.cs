using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Npgsql;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class MomentosIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private static async Task<int> PrepararRecuerdo(ApplicationDbContext db)
    {
        var publicacion = new Publicacion
        {
            Titulo = "Los festejos del 86", Fecha = new DateTime(1986, 6, 29, 0, 0, 0, DateTimeKind.Utc),
            Usuario = new Usuario_Personal { Username = "autor", Email = "momentos@test.com" },
            Lugar = new Lugar { Nombre = "Obelisco", Coordenadas = new Point(-58.3816, -34.6037) { SRID = 4326 } }
        };
        db.Publicaciones.Add(publicacion);
        await db.SaveChangesAsync();
        return publicacion.Id;
    }

    [Fact]
    public async Task QueSeGuardenMomentosAliasYVariasAsociacionesSinCambiarLaFechaDelRecuerdo()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var servicio = scope.ServiceProvider.GetRequiredService<IMomentoServicio>();
        var repositorio = scope.ServiceProvider.GetRequiredService<IMomentoRepositorio>();
        var publicacionId = await PrepararRecuerdo(db);
        var fecha = (await db.Publicaciones.SingleAsync()).Fecha;
        var mundial = await servicio.Crear(new Momento
        {
            Nombre = "Mundial de México 1986", FechaInicio = new DateOnly(1986, 5, 31), FechaFin = new DateOnly(1986, 6, 29)
        });
        var campeon = await servicio.Crear(new Momento { Nombre = "Argentina campeón 1986", MomentoPadreId = mundial });

        //Act
        await servicio.AgregarAlias(mundial, " México   86 ");
        await servicio.AgregarAlias(mundial, "MEXICO 86");
        await servicio.AsociarAutomaticamente(publicacionId, mundial, 0.9, "v1");
        await servicio.AsociarAutomaticamente(publicacionId, mundial, 0.95, "v2");
        await servicio.AsociarAutomaticamente(publicacionId, campeon, 0.9, "v2");
        db.ChangeTracker.Clear();
        var result = await servicio.ObtenerMomentosPublicacion(publicacionId);

        //Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(2, await db.PublicacionesMomentos.CountAsync());
        var guardado = await repositorio.ObtenerPorId(mundial);
        Assert.Equal(new DateOnly(1986, 5, 31), guardado!.FechaInicio);
        Assert.Equal("mexico 86", Assert.Single(guardado.Alias).TextoNormalizado);
        Assert.Equal(campeon, Assert.Single(await repositorio.ObtenerHijos(mundial)).Id);
        Assert.Equal(fecha, (await db.Publicaciones.SingleAsync()).Fecha);
        Assert.Equal("v2", (await repositorio.ObtenerAsociacion(publicacionId, mundial))!.VersionAnalisis);
    }

    [Fact]
    public async Task QueDosCambiosConcurrentesDePadreNoPuedanCrearUnCiclo()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        int primero, segundo;
        using (var scope = factory.Services.CreateScope())
        {
            var servicio = scope.ServiceProvider.GetRequiredService<IMomentoServicio>();
            primero = await servicio.Crear(new Momento { Nombre = "Primero" });
            segundo = await servicio.Crear(new Momento { Nombre = "Segundo" });
        }
        async Task<Exception?> Cambiar(int id, int padre)
        {
            using var scope = factory.Services.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IMomentoServicio>().CambiarPadre(id, padre);
                return null;
            }
            catch (ArgumentException error) { return error; }
        }

        //Act
        var result = await Task.WhenAll(Cambiar(primero, segundo), Cambiar(segundo, primero));

        //Assert
        Assert.Single(result, r => r == null);
        Assert.Contains("ciclos", Assert.IsType<ArgumentException>(Assert.Single(result, r => r != null)).Message);
        using var consulta = factory.Services.CreateScope();
        var db = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Momentos.CountAsync(m => m.MomentoPadreId == null));
    }

    [Fact]
    public async Task QueLasAsociacionesConcurrentesNoDupliquenNiSobrescribanAlAutor()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        int publicacionId, momentoId, autorId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            publicacionId = await PrepararRecuerdo(db);
            autorId = (await db.Publicaciones.SingleAsync()).UsuarioId;
            momentoId = await scope.ServiceProvider.GetRequiredService<IMomentoServicio>().Crear(new Momento { Nombre = "Mundial" });
        }
        async Task Asociar(bool autor)
        {
            using var scope = factory.Services.CreateScope();
            var servicio = scope.ServiceProvider.GetRequiredService<IMomentoServicio>();
            if (autor) await servicio.AsociarPorAutor(publicacionId, momentoId, autorId);
            else await servicio.AsociarAutomaticamente(publicacionId, momentoId, 0.9, "v1");
        }

        //Act
        await Task.WhenAll(Asociar(false), Asociar(true), Asociar(false));

        //Assert
        using var consulta = factory.Services.CreateScope();
        var guardada = await consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>().PublicacionesMomentos.SingleAsync();
        Assert.Equal(OrigenAsociacionMomento.Autor, guardada.Origen);
        Assert.Null(guardada.Confianza);
        Assert.Null(guardada.VersionAnalisis);
    }

    [Theory]
    [InlineData("fechas", "23514")]
    [InlineData("padrePropio", "23514")]
    [InlineData("padreInexistente", "23503")]
    [InlineData("confianza", "23514")]
    [InlineData("origen", "23514")]
    [InlineData("estado", "23514")]
    [InlineData("aliasRepetido", "23505")]
    [InlineData("asociacionRepetida", "23505")]
    public async Task QueLaBaseRechaceDatosQueViolanLasRestricciones(string caso, string sqlState)
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publicacionId = await PrepararRecuerdo(db);
        var momento = new Momento { Nombre = "Mundial" };
        db.Momentos.Add(momento);
        await db.SaveChangesAsync();
        db.MomentosAlias.Add(new MomentoAlias { MomentoId = momento.Id, Texto = "México 86", TextoNormalizado = "mexico 86" });
        db.PublicacionesMomentos.Add(new PublicacionMomento { PublicacionId = publicacionId, MomentoId = momento.Id });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        switch (caso)
        {
            case "fechas": db.Momentos.Add(new Momento { Nombre = "Inválido", FechaInicio = new DateOnly(1986, 6, 29), FechaFin = new DateOnly(1986, 5, 31) }); break;
            case "padrePropio": db.Momentos.Add(new Momento { Id = 99, Nombre = "Inválido", MomentoPadreId = 99 }); break;
            case "padreInexistente": db.Momentos.Add(new Momento { Nombre = "Inválido", MomentoPadreId = 999 }); break;
            case "aliasRepetido": db.MomentosAlias.Add(new MomentoAlias { MomentoId = momento.Id, Texto = "MEXICO 86", TextoNormalizado = "mexico 86" }); break;
            default:
                if (caso != "asociacionRepetida")
                {
                    // Eliminar el vínculo previo para aislar cada check de la restricción de unicidad.
                    await db.PublicacionesMomentos.ExecuteDeleteAsync();
                }
                db.PublicacionesMomentos.Add(new PublicacionMomento
                {
                    PublicacionId = publicacionId, MomentoId = momento.Id,
                    Confianza = caso == "confianza" ? 1.1 : null,
                    Origen = caso == "origen" ? (OrigenAsociacionMomento)99 : OrigenAsociacionMomento.Automatico,
                    Estado = caso == "estado" ? (EstadoAsociacionMomento)99 : EstadoAsociacionMomento.Activo
                });
                break;
        }

        //Act
        var result = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        //Assert
        Assert.Equal(sqlState, Assert.IsType<PostgresException>(result.InnerException).SqlState);
    }

    [Fact]
    public async Task QueEliminarUnMomentoSinReferenciasQuiteSusAlias()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var servicio = scope.ServiceProvider.GetRequiredService<IMomentoServicio>();
        var momentoId = await servicio.Crear(new Momento { Nombre = "Mundial" });
        await servicio.AgregarAlias(momentoId, "México 86");
        db.ChangeTracker.Clear();

        //Act
        await db.Momentos.Where(m => m.Id == momentoId).ExecuteDeleteAsync();

        //Assert
        Assert.Empty(await db.Momentos.ToListAsync());
        Assert.Empty(await db.MomentosAlias.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueNoSePuedaBorrarUnMomentoConHijosORecuerdosVinculados(bool conHijo)
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var servicio = scope.ServiceProvider.GetRequiredService<IMomentoServicio>();
        var momentoId = await servicio.Crear(new Momento { Nombre = "Mundial" });
        if (conHijo) await servicio.Crear(new Momento { Nombre = "Final", MomentoPadreId = momentoId });
        else
        {
            var publicacionId = await PrepararRecuerdo(db);
            await servicio.AsociarAutomaticamente(publicacionId, momentoId, 0.9, "v1");
        }
        db.ChangeTracker.Clear();

        //Act
        var result = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Momentos.Where(m => m.Id == momentoId).ExecuteDeleteAsync());

        //Assert
        Assert.Equal("23503", result.SqlState);
        Assert.True(await db.Momentos.AnyAsync(m => m.Id == momentoId));
    }

    [Fact]
    public async Task QueEliminarUnRecuerdoQuiteSusVinculosPeroConserveLosMomentos()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publicacionId = await PrepararRecuerdo(db);
        var servicio = scope.ServiceProvider.GetRequiredService<IMomentoServicio>();
        var momentoId = await servicio.Crear(new Momento { Nombre = "Mundial" });
        await servicio.AgregarAlias(momentoId, "México 86");
        await servicio.AsociarAutomaticamente(publicacionId, momentoId, 0.9, "v1");
        db.ChangeTracker.Clear();

        //Act
        await db.Publicaciones.Where(p => p.Id == publicacionId).ExecuteDeleteAsync();

        //Assert
        Assert.Empty(await db.PublicacionesMomentos.ToListAsync());
        Assert.Equal(momentoId, (await db.Momentos.SingleAsync()).Id);
        Assert.Single(await db.MomentosAlias.ToListAsync());
    }
}
