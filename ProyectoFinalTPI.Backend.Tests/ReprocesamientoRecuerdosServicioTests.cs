using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ReprocesamientoRecuerdosServicioTests
{
    private readonly Mock<IReprocesamientoRecuerdosRepositorio> _repositorio = new(MockBehavior.Strict);
    private readonly Mock<IAnalizadorRecuerdo> _analizador = new(MockBehavior.Strict);
    private readonly OpcionesAnalisisRecuerdos _opciones = new();
    private readonly ReprocesamientoRecuerdosServicio _servicio;
    private readonly RelojFijo _reloj = new();

    public ReprocesamientoRecuerdosServicioTests()
    {
        _analizador.SetupGet(a => a.Disponible).Returns(true);
        _servicio = new(_repositorio.Object, _analizador.Object, Options.Create(_opciones), _reloj, NullLogger<ReprocesamientoRecuerdosServicio>.Instance);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(26, 0)]
    [InlineData(10, -1)]
    public async Task QueNoSeConsulteUnaVistaPreviaConLimitesInvalidos(int limite, int despuesDeId)
    {
        //Arrange
        var ct = CancellationToken.None;

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Previsualizar(limite, despuesDeId, false, ct));

        //Assert
        _repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueSePuedaPrevisualizarAunqueElProveedorEsteDeshabilitado()
    {
        //Arrange
        _analizador.SetupGet(a => a.Disponible).Returns(false);
        var vista = new VistaPreviaReprocesamientoDto([new(10, 1, "Recuerdo", "noProgramado")], false, null);
        _repositorio.Setup(r => r.Previsualizar(10, 0, false, CancellationToken.None)).ReturnsAsync(vista);

        //Act
        var result = await _servicio.Previsualizar(10, 0, false, CancellationToken.None);

        //Assert
        Assert.False(result.AnalisisDisponible);
        Assert.Same(vista.Recuerdos, result.Recuerdos);
        _repositorio.VerifyAll();
    }

    [Fact]
    public async Task QueSeEncoleLaSeleccionExactaConLaHoraDelServidor()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();
        var solicitud = new SolicitudReprocesamientoDto([new(10, 3)], IncluirFallidos: true, IntervaloSegundos: 20);
        var resultado = new ResultadoReprocesamientoDto([new(10, 3, "reprogramado", ProximoIntento: _reloj.GetUtcNow().UtcDateTime.AddSeconds(20))]);
        _repositorio.Setup(r => r.Encolar(solicitud, _reloj.GetUtcNow().UtcDateTime, cancelacion.Token)).ReturnsAsync(resultado);

        //Act
        var result = await _servicio.Encolar(solicitud, 7, cancelacion.Token);

        //Assert
        Assert.Same(resultado, result);
        Assert.Equal(1, result.Programados);
        _repositorio.VerifyAll();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task QueSeRechacenLotesInvalidosAntesDeAccederAlRepositorio(int caso)
    {
        //Arrange
        SolicitudReprocesamientoDto solicitud = caso switch
        {
            0 => new([]),
            1 => new(Enumerable.Range(1, 26).Select(i => new RecuerdoAReprocesarDto(i, 1)).ToArray()),
            2 => new([new(10, 1), new(10, 2)]),
            3 => new([new(0, 1)]),
            4 => new([new(10, 0)]),
            5 => new([new(10, 1)], IntervaloSegundos: 9),
            6 => new([new(10, 1)], IntervaloSegundos: 3601),
            7 => new(null!),
            _ => new([null!])
        };

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Encolar(solicitud, 7, CancellationToken.None));

        //Assert
        _repositorio.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task QueNoSeEncoleCuandoFaltaElAnalizadorOElWorker(bool proveedorDisponible, bool workerHabilitado)
    {
        //Arrange
        _analizador.SetupGet(a => a.Disponible).Returns(proveedorDisponible);
        _opciones.Habilitado = workerHabilitado;

        //Act
        await Assert.ThrowsAsync<AnalisisNoDisponibleException>(() => _servicio.Encolar(new([new(10, 1)]), 7, CancellationToken.None));

        //Assert
        _repositorio.VerifyNoOtherCalls();
    }

    private sealed class RelojFijo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    }
}
