using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public partial class TrabajoAnalisisRepositorioTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TrabajoAnalisisRepositorio _repositorio;
    private readonly DateTime _ahora = DateTime.UtcNow.AddSeconds(1);

    public TrabajoAnalisisRepositorioTests()
    {
        _context = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _repositorio = new(_context);
    }

    private async Task Preparar()
    {
        _context.Publicaciones.Add(new Publicacion { Id = 10, Titulo = "México 86", Descripcion = "Los festejos", Fecha = new DateTime(1986, 6, 29, 0, 0, 0, DateTimeKind.Utc) });
        _context.Momentos.Add(new Momento { Id = 1, Nombre = "Mundial" });
        await _context.SaveChangesAsync();
        // La hora de elegibilidad del fixture no depende de cuánto tarde EF en inicializarse.
        (await _context.TrabajosAnalisisRecuerdos.SingleAsync()).ProximoIntento = _ahora;
        await _context.SaveChangesAsync();
    }
    private Task<ReservaAnalisisDto?> Reservar(DateTime? ahora = null, int maxIntentos = 3) =>
        _repositorio.Reservar(ahora ?? _ahora, TimeSpan.FromSeconds(120), maxIntentos, CancellationToken.None);

    [Fact]
    public async Task QueGuardarUnRecuerdoGenereUnTrabajoDeSuVersionInicial()
    {
        //Arrange
        var publicacion = new Publicacion { Id = 10, Titulo = "Recuerdo" };
        _context.Publicaciones.Add(publicacion);

        //Act
        await _context.SaveChangesAsync();

        //Assert
        var result = await _context.TrabajosAnalisisRecuerdos.SingleAsync();
        Assert.Equal(10, result.PublicacionId);
        Assert.Equal(1, result.VersionContenido);
        Assert.Equal(EstadoTrabajoAnalisis.Pendiente, result.Estado);
        Assert.Equal(0, result.Intentos);
        Assert.Null(result.TokenReserva);
    }

    [Fact]
    public async Task QueEditarContenidoGenereOtraVersionPeroCambiarRelacionesNoGenereTrabajos()
    {
        //Arrange
        await Preparar();
        var publicacion = await _context.Publicaciones.SingleAsync();

        //Act
        publicacion.Descripcion = "Otra descripción";
        await _context.SaveChangesAsync();
        publicacion.VersionRelaciones++;
        await _context.SaveChangesAsync();

        //Assert
        Assert.Equal(2, publicacion.VersionContenido);
        Assert.Equal(new long[] { 1, 2 }, await _context.TrabajosAnalisisRecuerdos.OrderBy(j => j.VersionContenido).Select(j => j.VersionContenido).ToArrayAsync());
    }

    [Fact]
    public async Task QueNoSeEncolenRepostsNiRecuerdosOcultos()
    {
        //Arrange
        _context.Publicaciones.AddRange(new Publicacion { Id = 10, PublicacionOriginalId = 99 }, new Publicacion { Id = 11, EstaOculto = true });

        //Act
        await _context.SaveChangesAsync();

        //Assert
        Assert.Empty(await _context.TrabajosAnalisisRecuerdos.ToListAsync());
    }

    [Fact]
    public async Task QueUnTrabajoReservadoNoSeTomeDeNuevoHastaQueVenza()
    {
        //Arrange
        await Preparar();

        //Act
        var primera = await Reservar();
        var segunda = await Reservar();

        //Assert
        Assert.NotNull(primera);
        Assert.Equal(1, primera.Intentos);
        Assert.NotEqual(Guid.Empty, primera.TokenReserva);
        Assert.Null(segunda);
        Assert.Equal(EstadoTrabajoAnalisis.Procesando, (await _context.TrabajosAnalisisRecuerdos.SingleAsync()).Estado);
    }

    [Fact]
    public async Task QueSeRecupereUnaReservaVencidaYSeRechaceSuTokenAnterior()
    {
        //Arrange
        await Preparar();
        var primera = (await Reservar())!;
        var despues = _ahora.AddSeconds(121);

        //Act
        var recuperada = (await Reservar(despues))!;
        var aceptado = await _repositorio.Completar(primera, new(new[] { new MomentoDetectadoDto(1, 0.9) }), "v1", despues, CancellationToken.None);

        //Assert
        Assert.Equal(primera.Id, recuperada.Id);
        Assert.Equal(2, recuperada.Intentos);
        Assert.NotEqual(primera.TokenReserva, recuperada.TokenReserva);
        Assert.False(aceptado);
        Assert.Empty(await _context.PublicacionesMomentos.ToListAsync());
    }

    [Fact]
    public async Task QueUnTrabajoInterrumpidoNoSeReintenteIndefinidamente()
    {
        //Arrange
        await Preparar();
        await Reservar(maxIntentos: 1);

        //Act
        var result = await Reservar(_ahora.AddSeconds(121), 1);

        //Assert
        Assert.Null(result);
        var trabajo = await _context.TrabajosAnalisisRecuerdos.SingleAsync();
        Assert.Equal(EstadoTrabajoAnalisis.Fallido, trabajo.Estado);
        Assert.Null(trabajo.TokenReserva);
        Assert.Equal("reintentos_agotados", trabajo.CodigoError);
    }

    [Fact]
    public async Task QueElReintentoRespeteLaFechaProgramada()
    {
        //Arrange
        await Preparar();
        var reserva = (await Reservar())!;
        var siguiente = _ahora.AddSeconds(10);

        //Act
        await _repositorio.RegistrarFallo(reserva, "analisis_error", _ahora, siguiente, 3, CancellationToken.None);
        var antes = await Reservar(_ahora.AddSeconds(9));
        var despues = await Reservar(siguiente);

        //Assert
        Assert.Null(antes);
        Assert.Equal(2, despues!.Intentos);
    }

    [Fact]
    public async Task QueCompletarElTrabajoGuardeLasAsociacionesYNoSeRepita()
    {
        //Arrange
        await Preparar();
        var reserva = (await Reservar())!;
        var resultado = new ResultadoAnalisisRecuerdoDto(new[] { new MomentoDetectadoDto(1, 0.9) });

        //Act
        var primera = await _repositorio.Completar(reserva, resultado, "v1", _ahora, CancellationToken.None);
        var segunda = await _repositorio.Completar(reserva, resultado, "v1", _ahora, CancellationToken.None);

        //Assert
        Assert.True(primera);
        Assert.False(segunda);
        Assert.Equal(1, (await _context.Publicaciones.SingleAsync()).VersionRelaciones);
        Assert.Equal(1, (await _context.PublicacionesMomentos.SingleAsync()).MomentoId);
        Assert.Equal(EstadoTrabajoAnalisis.Completado, (await _context.TrabajosAnalisisRecuerdos.SingleAsync()).Estado);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueNoSeApliqueElResultadoSiCambioElContenidoOSeDesactivoElAnalisis(bool cambiarContenido)
    {
        //Arrange
        await Preparar();
        var reserva = (await Reservar())!;
        var publicacion = await _context.Publicaciones.SingleAsync();
        if (cambiarContenido) publicacion.Titulo = "Texto modificado";
        else publicacion.PermitirMomentosAutomaticos = false;
        await _context.SaveChangesAsync();

        //Act
        var result = await _repositorio.Completar(reserva, new(new[] { new MomentoDetectadoDto(1, 0.9) }), "v1", _ahora, CancellationToken.None);

        //Assert
        Assert.False(result);
        Assert.Empty(await _context.PublicacionesMomentos.ToListAsync());
        Assert.Equal(EstadoTrabajoAnalisis.Descartado, (await _context.TrabajosAnalisisRecuerdos.SingleAsync(j => j.Id == reserva.Id)).Estado);
    }

    [Theory]
    [InlineData(EstadoAsociacionMomento.Activo)]
    [InlineData(EstadoAsociacionMomento.Rechazado)]
    public async Task QueElResultadoAutomaticoRespeteLasDecisionesDelAutor(EstadoAsociacionMomento estado)
    {
        //Arrange
        await Preparar();
        var reserva = (await Reservar())!;
        _context.PublicacionesMomentos.Add(new PublicacionMomento { PublicacionId = 10, MomentoId = 1, Origen = OrigenAsociacionMomento.Autor, Estado = estado });
        await _context.SaveChangesAsync();

        //Act
        await _repositorio.Completar(reserva, new(new[] { new MomentoDetectadoDto(1, 0.99) }), "v1", _ahora, CancellationToken.None);

        //Assert
        var result = await _context.PublicacionesMomentos.SingleAsync();
        Assert.Equal(OrigenAsociacionMomento.Autor, result.Origen);
        Assert.Equal(estado, result.Estado);
        Assert.Null(result.Confianza);
    }

    [Fact]
    public async Task QueElAnalizadorSinProveedorNoQuiteAsociacionesExistentes()
    {
        //Arrange
        await Preparar();
        var reserva = (await Reservar())!;
        _context.PublicacionesMomentos.Add(new PublicacionMomento { PublicacionId = 10, MomentoId = 1 });
        await _context.SaveChangesAsync();

        //Act
        await _repositorio.Completar(reserva, new(Array.Empty<MomentoDetectadoDto>(), AnalisisRealizado: false), "sin-proveedor-v1", _ahora, CancellationToken.None);

        //Assert
        Assert.Single(await _context.PublicacionesMomentos.ToListAsync());
        Assert.Equal("sinAnalizador", (await _repositorio.ObtenerEstado(10, CancellationToken.None))!.Estado);
    }

    [Fact]
    public async Task QueUnAnalisisRealSinCoincidenciasRetireSoloLosVinculosAutomaticos()
    {
        //Arrange
        await Preparar();
        var reserva = (await Reservar())!;
        _context.PublicacionesMomentos.Add(new PublicacionMomento { PublicacionId = 10, MomentoId = 1 });
        await _context.SaveChangesAsync();

        //Act
        await _repositorio.Completar(reserva, new(Array.Empty<MomentoDetectadoDto>()), "v1", _ahora, CancellationToken.None);

        //Assert
        Assert.Empty(await _context.PublicacionesMomentos.ToListAsync());
        Assert.Equal("sinMomento", (await _repositorio.ObtenerEstado(10, CancellationToken.None))!.Estado);
        Assert.Equal(1, (await _context.Publicaciones.SingleAsync()).VersionRelaciones);
    }

    public void Dispose() => _context.Dispose();
}
