using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public partial class TrabajoAnalisisRepositorioTests
{
    private static ResultadoAnalisisRecuerdoDto Proponer(string nombre = "Argentina campeón del mundo 1986", int anio = 1986,
        int momentoId = 0, string[]? alias = null) => new([], Propuestas:
        [new(momentoId, nombre, anio, alias ?? ["México 86"], 0.95, "México 86")]);

    [Fact]
    public async Task QueSeCreeUnMomentoColectivoConAliasYSeAsocieEnElMismoTrabajo()
    {
        //Arrange
        await Preparar();
        var reserva = (await Reservar())!;

        //Act
        await _repositorio.Completar(reserva, Proponer(), "gemini:test:v1", _ahora, CancellationToken.None);

        //Assert
        var momento = await _context.Momentos.Include(m => m.Alias).SingleAsync(m => m.Id != 1);
        Assert.Equal("Argentina campeón del mundo 1986", momento.Nombre);
        Assert.Equal(new DateOnly(1986, 1, 1), momento.FechaInicio);
        Assert.Contains(momento.Alias, a => a.TextoNormalizado == "mexico 86");
        Assert.Equal(momento.Id, (await _context.PublicacionesMomentos.SingleAsync()).MomentoId);
        Assert.Equal("completado", (await _repositorio.ObtenerEstado(10, CancellationToken.None))!.Estado);
    }

    [Fact]
    public async Task QueUnNombreCanonicoYaExistenteSeReutiliceAunqueLaPublicacionUseOtroTexto()
    {
        //Arrange
        await Preparar();
        _context.Momentos.Add(new Momento { Id = 25, Nombre = "Argentina campeón del mundo 1986" });
        await _context.SaveChangesAsync();
        var reserva = (await Reservar())!;

        //Act
        await _repositorio.Completar(reserva, Proponer(), "v1", _ahora, CancellationToken.None);

        //Assert
        Assert.Equal(2, await _context.Momentos.CountAsync());
        Assert.Equal(25, (await _context.PublicacionesMomentos.SingleAsync()).MomentoId);
    }

    [Fact]
    public async Task QueSeReutiliceUnAliasCanonicoNormalizadoSinConfundirLosAnios()
    {
        //Arrange
        await Preparar();
        _context.Momentos.Add(new Momento
        {
            Id = 25, Nombre = "El campeonato argentino de 1986", FechaInicio = new DateOnly(1986, 1, 1),
            FechaFin = new DateOnly(1986, 12, 31), Alias = [new() { Texto = "Argentina campeón del mundo 1986", TextoNormalizado = "argentina campeon del mundo 1986" }]
        });
        await _context.SaveChangesAsync();
        var reserva = (await Reservar())!;

        //Act
        await _repositorio.Completar(reserva, Proponer(), "v1", _ahora, CancellationToken.None);

        //Assert
        Assert.Equal(25, (await _context.PublicacionesMomentos.SingleAsync()).MomentoId);
        Assert.Equal(2, await _context.Momentos.CountAsync());
    }

    [Fact]
    public async Task QueArgentinaCampeon1986YArgentinaCampeon2022SeanMomentosDistintos()
    {
        //Arrange
        await Preparar();
        _context.Momentos.Add(new Momento { Id = 25, Nombre = "Argentina campeón del mundo 1986" });
        await _context.SaveChangesAsync();
        var reserva = (await Reservar())!;

        //Act
        await _repositorio.Completar(reserva, Proponer("Argentina campeón del mundo 2022", 2022, alias: ["Qatar 2022"]), "v1", _ahora, CancellationToken.None);

        //Assert
        var asociacion = await _context.PublicacionesMomentos.Include(a => a.Momento).SingleAsync();
        Assert.NotEqual(25, asociacion.MomentoId);
        Assert.Equal("Argentina campeón del mundo 2022", asociacion.Momento.Nombre);
    }

    [Fact]
    public async Task QueDosPropuestasQueResuelvenAlMismoMomentoGenerenUnSoloVinculo()
    {
        //Arrange
        await Preparar();
        var reserva = (await Reservar())!;
        var propuesta = Proponer().Propuestas!.Single();
        var resultado = new ResultadoAnalisisRecuerdoDto([], Propuestas: [propuesta, propuesta with { Confianza = 0.99 }]);

        //Act
        await _repositorio.Completar(reserva, resultado, "v1", _ahora, CancellationToken.None);

        //Assert
        Assert.Equal(2, await _context.Momentos.CountAsync());
        Assert.Equal(0.99, (await _context.PublicacionesMomentos.SingleAsync()).Confianza);
    }

    [Fact]
    public async Task QueNoSeCreeCatalogoParaUnResultadoDesactualizado()
    {
        //Arrange
        await Preparar();
        var reserva = (await Reservar())!;
        (await _context.Publicaciones.SingleAsync()).Titulo = "Texto nuevo";
        await _context.SaveChangesAsync();

        //Act
        var result = await _repositorio.Completar(reserva, Proponer(), "v1", _ahora, CancellationToken.None);

        //Assert
        Assert.False(result);
        Assert.Equal(1, await _context.Momentos.CountAsync());
        Assert.Empty(await _context.PublicacionesMomentos.ToListAsync());
    }

    [Fact]
    public async Task QueLaDeteccionPorNombreNoRestaureUnMomentoRechazadoPorElAutor()
    {
        //Arrange
        await Preparar();
        var momento = await _context.Momentos.SingleAsync();
        momento.Nombre = "Argentina campeón del mundo 1986";
        _context.PublicacionesMomentos.Add(new() { PublicacionId = 10, MomentoId = 1, Origen = OrigenAsociacionMomento.Autor, Estado = EstadoAsociacionMomento.Rechazado });
        await _context.SaveChangesAsync();
        var reserva = (await Reservar())!;

        //Act
        await _repositorio.Completar(reserva, Proponer(), "v1", _ahora, CancellationToken.None);

        //Assert
        Assert.Equal(1, await _context.Momentos.CountAsync());
        Assert.Equal(EstadoAsociacionMomento.Rechazado, (await _context.PublicacionesMomentos.SingleAsync()).Estado);
    }
}
