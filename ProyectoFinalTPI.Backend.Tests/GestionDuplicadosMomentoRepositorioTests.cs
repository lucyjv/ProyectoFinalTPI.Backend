using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class GestionDuplicadosMomentoRepositorioTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly MomentoRepositorio _repositorio;
    public GestionDuplicadosMomentoRepositorioTests() => _repositorio = new(_context);

    private async Task Preparar()
    {
        _context.Momentos.AddRange(new Momento { Id = 1, Nombre = "México 86", FechaInicio = new(1986, 1, 1), FechaFin = new(1986, 12, 31),
                Alias = [new() { Texto = "Argentina campeón 1986", TextoNormalizado = "argentina campeon 1986" }] },
            new Momento { Id = 2, Nombre = "Argentina campeón 1986", FechaInicio = new(1986, 1, 1), FechaFin = new(1986, 12, 31) },
            new Momento { Id = 3, Nombre = "Argentina campeón 2022", FechaInicio = new(2022, 1, 1), FechaFin = new(2022, 12, 31) });
        _context.Publicaciones.AddRange(new Publicacion { Id = 10, Titulo = "Recuerdo" }, new Publicacion { Id = 11, Titulo = "Otro recuerdo" });
        _context.PublicacionesMomentos.AddRange(new() { PublicacionId = 10, MomentoId = 1, Origen = OrigenAsociacionMomento.Autor, Estado = EstadoAsociacionMomento.Rechazado },
            new() { PublicacionId = 10, MomentoId = 2, Origen = OrigenAsociacionMomento.Autor },
            new() { PublicacionId = 11, MomentoId = 1, Confianza = 0.9, VersionAnalisis = "v1" });
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task QueSeSugieranCoincidenciasPorAliasSinConfundirAniosNiModificarElCatalogo()
    {
        //Arrange
        await Preparar();

        //Act
        var result = await _repositorio.BuscarCandidatos(1, 20, CancellationToken.None);

        //Assert
        Assert.Equal(2, Assert.Single(result).Momento.Id);
        Assert.Equal(1, result[0].Similitud);
        Assert.Equal(3, await _context.Momentos.CountAsync());
        Assert.All(await _context.Momentos.ToListAsync(), m => Assert.Null(m.MomentoCanonicoId));
    }

    [Fact]
    public async Task QueLaVistaPreviaInformeElImpactoYElConflictoDelAutorSinFusionar()
    {
        //Arrange
        await Preparar();

        //Act
        var result = await _repositorio.Previsualizar(1, 2, CancellationToken.None);

        //Assert
        Assert.Equal(2, result.PublicacionesAfectadas);
        Assert.Equal(1, result.DecisionesEnConflicto);
        Assert.Equal(1, result.Origen.VersionCatalogo);
        Assert.Null((await _context.Momentos.FindAsync(1))!.MomentoCanonicoId);
    }

    [Fact]
    public async Task QueFusionarConserveAliasMuevaVinculosYDePrioridadAlRechazoDelAutor()
    {
        //Arrange
        await Preparar();

        //Act
        var result = await _repositorio.Fusionar(1, new(2, 1, 1), CancellationToken.None);

        //Assert
        Assert.Equal(2, result.DestinoId);
        Assert.Equal(2, result.PublicacionesAfectadas);
        Assert.Equal(1, result.DecisionesEnConflicto);
        Assert.Equal(2, (await _context.Momentos.FindAsync(1))!.MomentoCanonicoId);
        Assert.Null((await _context.Momentos.FindAsync(1))!.ClaveIdentidad);
        Assert.Equal(2, await _context.PublicacionesMomentos.CountAsync());
        Assert.All(await _context.PublicacionesMomentos.ToListAsync(), a => Assert.Equal(2, a.MomentoId));
        var autor = await _context.PublicacionesMomentos.SingleAsync(a => a.PublicacionId == 10);
        Assert.Equal(OrigenAsociacionMomento.Autor, autor.Origen);
        Assert.Equal(EstadoAsociacionMomento.Rechazado, autor.Estado);
        Assert.Equal(0.9, (await _context.PublicacionesMomentos.SingleAsync(a => a.PublicacionId == 11)).Confianza);
        Assert.All(await _context.Publicaciones.ToListAsync(), p => { Assert.Equal(1, p.VersionRelaciones); Assert.Equal(1, p.VersionContenido); });
        Assert.Equal(2, await _context.TrabajosAnalisisRecuerdos.CountAsync());
        Assert.Contains((await _repositorio.ObtenerPorId(2))!.Alias, a => a.Texto == "México 86");
    }

    [Fact]
    public async Task QueRepetirUnaFusionNoVuelvaAIncrementarVersionesNiDuplicarVinculos()
    {
        //Arrange
        await Preparar();
        await _repositorio.Fusionar(1, new(2, 1, 1), CancellationToken.None);

        //Act
        var result = await _repositorio.Fusionar(1, new(2, 1, 1), CancellationToken.None);

        //Assert
        Assert.True(result.YaFusionado);
        Assert.Equal(0, result.PublicacionesAfectadas);
        Assert.Equal(2, await _context.PublicacionesMomentos.CountAsync());
        Assert.All(await _context.Publicaciones.ToListAsync(), p => Assert.Equal(1, p.VersionRelaciones));
    }

    [Fact]
    public async Task QueLosIdsAnterioresSeResuelvanAlCanonicoYNoAparezcanEnElListado()
    {
        //Arrange
        await Preparar();
        await _repositorio.Fusionar(1, new(2, 1, 1), CancellationToken.None);

        //Act
        var detalle = await _repositorio.ObtenerDetalle(1, CancellationToken.None);
        var listado = await _repositorio.Buscar("", "", null, 50, CancellationToken.None);

        //Assert
        Assert.Equal(2, detalle!.Id);
        Assert.Equal(1, detalle.RedirigidoDesdeId);
        Assert.DoesNotContain(listado, m => m.Id == 1);
        Assert.Equal(2, (await _repositorio.ObtenerPorId(1))!.Id);
    }

    [Fact]
    public async Task QueCrearConUnNombreFusionadoReutiliceElMomentoCanonico()
    {
        //Arrange
        await Preparar();
        await _repositorio.Fusionar(1, new(2, 1, 1), CancellationToken.None);

        //Act
        var result = await _repositorio.Crear(new Momento { Nombre = "MÉXICO 86", FechaInicio = new(1986, 1, 1), FechaFin = new(1986, 12, 31) });

        //Assert
        Assert.Equal(2, result);
        Assert.Equal(3, await _context.Momentos.CountAsync());
    }

    [Fact]
    public async Task QueSeReutiliceUnaIdentidadNormalizadaSinAcentosMayusculasOPuntuacion()
    {
        //Arrange
        var primero = await _repositorio.Crear(new Momento { Nombre = "México-1986" });

        //Act
        var result = await _repositorio.Crear(new Momento { Nombre = "  MEXICO 1986  " });

        //Assert
        Assert.Equal(primero, result);
        Assert.Equal(1, await _context.Momentos.CountAsync());
    }

    [Fact]
    public async Task QueUnaVersionDeCatalogoViejaNoPermitaFusionar()
    {
        //Arrange
        await Preparar();
        (await _context.Momentos.FindAsync(2))!.Nombre = "Argentina campeón del mundo 1986";
        await _context.SaveChangesAsync();

        //Act
        await Assert.ThrowsAsync<ConflictoCatalogoException>(() => _repositorio.Fusionar(1, new(2, 1, 1), CancellationToken.None));

        //Assert
        Assert.Null((await _context.Momentos.FindAsync(1))!.MomentoCanonicoId);
        Assert.Equal(3, await _context.PublicacionesMomentos.CountAsync());
    }

    [Fact]
    public async Task QueNoSeFusionenEdicionesDeDistintosAnios()
    {
        //Arrange
        await Preparar();

        //Act
        await Assert.ThrowsAsync<ConflictoCatalogoException>(() => _repositorio.Fusionar(1, new(3, 1, 1), CancellationToken.None));

        //Assert
        Assert.Null((await _context.Momentos.FindAsync(1))!.MomentoCanonicoId);
    }

    [Fact]
    public async Task QueSeTrasladenLosHijosSinCrearCiclosYSeResuelvanCadenasDeFusiones()
    {
        //Arrange
        await Preparar();
        _context.Momentos.AddRange(new Momento { Id = 4, Nombre = "Los festejos", MomentoPadreId = 1 }, new Momento { Id = 5, Nombre = "El triunfo argentino 1986" });
        await _context.SaveChangesAsync();
        await _repositorio.Fusionar(1, new(2, 1, 1), CancellationToken.None);

        //Act
        await _repositorio.Fusionar(2, new(5, (await _context.Momentos.FindAsync(2))!.VersionCatalogo, 1), CancellationToken.None);

        //Assert
        Assert.Equal(5, (await _context.Momentos.FindAsync(4))!.MomentoPadreId);
        Assert.Equal(5, (await _context.Momentos.FindAsync(1))!.MomentoCanonicoId);
        Assert.Equal(5, (await _repositorio.ObtenerPorId(1))!.Id);
    }

    [Fact]
    public async Task QueNoSePuedanFusionarUnAncestroYSuDescendiente()
    {
        //Arrange
        await Preparar();
        (await _context.Momentos.FindAsync(2))!.MomentoPadreId = 1;
        await _context.SaveChangesAsync();

        //Act
        await Assert.ThrowsAsync<ConflictoCatalogoException>(() => _repositorio.Previsualizar(1, 2, CancellationToken.None));

        //Assert
        Assert.Null((await _context.Momentos.FindAsync(1))!.MomentoCanonicoId);
    }

    public void Dispose() => _context.Dispose();
}
