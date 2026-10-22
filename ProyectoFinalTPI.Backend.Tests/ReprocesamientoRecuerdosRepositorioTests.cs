using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ReprocesamientoRecuerdosRepositorioTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly ReprocesamientoRecuerdosRepositorio _repositorio;
    private readonly DateTime _ahora = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    public ReprocesamientoRecuerdosRepositorioTests() => _repositorio = new(_context);

    private async Task Preparar(int id, EstadoTrabajoAnalisis? estado = null)
    {
        _context.Publicaciones.Add(new Publicacion { Id = id, Titulo = $"Recuerdo {id}", Fecha = _ahora });
        await _context.SaveChangesAsync();
        var trabajo = await _context.TrabajosAnalisisRecuerdos.SingleAsync(j => j.PublicacionId == id);
        if (estado == null) _context.TrabajosAnalisisRecuerdos.Remove(trabajo);
        else
        {
            trabajo.Estado = estado.Value; trabajo.Intentos = 3;
            if (estado == EstadoTrabajoAnalisis.Procesando) { trabajo.TokenReserva = Guid.NewGuid(); trabajo.ReservadoHasta = _ahora.AddMinutes(2); }
        }
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task QueLaVistaPreviaNoEncoleYSePuedaContinuarSinRepetirRecuerdos()
    {
        //Arrange
        await Preparar(10);
        await Preparar(20, EstadoTrabajoAnalisis.SinAnalizador);
        await Preparar(30, EstadoTrabajoAnalisis.Fallido);

        //Act
        var primera = await _repositorio.Previsualizar(1, 0, false, CancellationToken.None);
        var segunda = await _repositorio.Previsualizar(1, primera.SiguienteDespuesDeId!.Value, false, CancellationToken.None);

        //Assert
        Assert.Equal(10, Assert.Single(primera.Recuerdos).PublicacionId);
        Assert.True(primera.HayMas);
        Assert.Equal("sinAnalizador", Assert.Single(segunda.Recuerdos).Estado);
        Assert.False(segunda.HayMas);
        Assert.Equal(2, await _context.TrabajosAnalisisRecuerdos.CountAsync());
        Assert.Equal(0, await _context.TrabajosAnalisisRecuerdos.CountAsync(j => j.Estado == EstadoTrabajoAnalisis.Pendiente));
    }

    [Theory]
    [InlineData(EstadoTrabajoAnalisis.Completado)]
    [InlineData(EstadoTrabajoAnalisis.SinMomento)]
    [InlineData(EstadoTrabajoAnalisis.Pendiente)]
    [InlineData(EstadoTrabajoAnalisis.Procesando)]
    [InlineData(EstadoTrabajoAnalisis.Descartado)]
    public async Task QueNoSeReprogramenTrabajosYaAnalizadosProgramadosODescartados(EstadoTrabajoAnalisis estado)
    {
        //Arrange
        await Preparar(10, estado);

        //Act
        var vista = await _repositorio.Previsualizar(10, 0, true, CancellationToken.None);
        var result = await _repositorio.Encolar(new([new(10, 1)], IncluirFallidos: true), _ahora, CancellationToken.None);

        //Assert
        Assert.Empty(vista.Recuerdos);
        Assert.Equal(0, result.Programados);
        Assert.Equal("omitido", Assert.Single(result.Recuerdos).Resultado);
        var trabajo = await _context.TrabajosAnalisisRecuerdos.SingleAsync();
        Assert.Equal(estado, trabajo.Estado);
        Assert.Equal(3, trabajo.Intentos);
    }

    [Fact]
    public async Task QueSeCreeElTrabajoFaltanteYSeReinicieElSinProveedorSinTocarRelaciones()
    {
        //Arrange
        await Preparar(10);
        await Preparar(20, EstadoTrabajoAnalisis.SinAnalizador);
        var anterior = await _context.TrabajosAnalisisRecuerdos.SingleAsync();
        anterior.CodigoError = "anterior"; anterior.VersionAnalizador = "sin-proveedor-v1";
        _context.PublicacionesMomentos.Add(new() { PublicacionId = 10, MomentoId = 99, Origen = OrigenAsociacionMomento.Autor, Estado = EstadoAsociacionMomento.Rechazado });
        await _context.SaveChangesAsync();

        //Act
        var result = await _repositorio.Encolar(new([new(20, 1), new(10, 1)], IntervaloSegundos: 15), _ahora, CancellationToken.None);

        //Assert
        Assert.Equal(2, result.Programados);
        Assert.Equal("encolado", result.Recuerdos[0].Resultado);
        Assert.Equal("reprogramado", result.Recuerdos[1].Resultado);
        Assert.Equal(_ahora.AddSeconds(15), result.Recuerdos[0].ProximoIntento);
        Assert.Equal(_ahora.AddSeconds(30), result.Recuerdos[1].ProximoIntento);
        Assert.Equal(0, anterior.Intentos);
        Assert.Null(anterior.CodigoError);
        Assert.Null(anterior.VersionAnalizador);
        Assert.Equal(EstadoAsociacionMomento.Rechazado, (await _context.PublicacionesMomentos.SingleAsync()).Estado);
        Assert.All(await _context.Publicaciones.ToListAsync(), p => { Assert.Equal(1, p.VersionContenido); Assert.Equal(0, p.VersionRelaciones); });
    }

    [Fact]
    public async Task QueRepetirElLoteNoDupliqueNiPospongaTrabajos()
    {
        //Arrange
        await Preparar(10);
        var solicitud = new SolicitudReprocesamientoDto([new(10, 1)]);
        var primero = await _repositorio.Encolar(solicitud, _ahora, CancellationToken.None);

        //Act
        var result = await _repositorio.Encolar(solicitud, _ahora.AddSeconds(2), CancellationToken.None);

        //Assert
        Assert.Equal(0, result.Programados);
        Assert.Equal("yaProgramado", Assert.Single(result.Recuerdos).Motivo);
        Assert.Equal(primero.Recuerdos[0].ProximoIntento, (await _context.TrabajosAnalisisRecuerdos.SingleAsync()).ProximoIntento);
    }

    [Fact]
    public async Task QueLosFallidosSoloEntrenCuandoSeSoliciteExpresamente()
    {
        //Arrange
        await Preparar(10, EstadoTrabajoAnalisis.Fallido);

        //Act
        var sinIncluir = await _repositorio.Encolar(new([new(10, 1)]), _ahora, CancellationToken.None);
        var vista = await _repositorio.Previsualizar(10, 0, true, CancellationToken.None);
        var result = await _repositorio.Encolar(new([new(10, 1)], IncluirFallidos: true), _ahora, CancellationToken.None);

        //Assert
        Assert.Equal("fallidoNoIncluido", Assert.Single(sinIncluir.Recuerdos).Motivo);
        Assert.Equal("fallido", Assert.Single(vista.Recuerdos).Estado);
        Assert.Equal(1, result.Programados);
        Assert.Equal(0, (await _context.TrabajosAnalisisRecuerdos.SingleAsync()).Intentos);
    }

    [Theory]
    [InlineData("oculto", "noDisponible")]
    [InlineData("repost", "noDisponible")]
    [InlineData("desactivado", "automatizacionDesactivada")]
    [InlineData("editado", "versionDesactualizada")]
    public async Task QueSeRevalideElRecuerdoSiCambioDespuesDeLaVistaPrevia(string cambio, string motivo)
    {
        //Arrange
        await Preparar(10);
        var p = await _context.Publicaciones.SingleAsync();
        if (cambio == "oculto") p.EstaOculto = true;
        if (cambio == "repost") p.PublicacionOriginalId = 99;
        if (cambio == "desactivado") p.PermitirMomentosAutomaticos = false;
        if (cambio == "editado") p.Titulo = "Texto modificado";
        await _context.SaveChangesAsync();

        //Act
        var result = await _repositorio.Encolar(new([new(10, 1)]), _ahora, CancellationToken.None);

        //Assert
        Assert.Equal(0, result.Programados);
        Assert.Equal(motivo, Assert.Single(result.Recuerdos).Motivo);
        Assert.False(await _context.TrabajosAnalisisRecuerdos.AnyAsync(j => j.PublicacionId == 10 && j.VersionContenido == 1));
    }

    [Fact]
    public async Task QueLosLotesConsecutivosRespetenLosHorariosYaReservadosEnLaCola()
    {
        //Arrange
        await Preparar(10);
        await Preparar(20);
        await _repositorio.Encolar(new([new(10, 1)]), _ahora, CancellationToken.None);

        //Act
        var result = await _repositorio.Encolar(new([new(20, 1)]), _ahora, CancellationToken.None);

        //Assert
        Assert.Equal(_ahora.AddSeconds(20), Assert.Single(result.Recuerdos).ProximoIntento);
    }

    public void Dispose() => _context.Dispose();
}
