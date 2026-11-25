using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class RankingMusicaSaldoTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueUnFalloDeLecturaDeSaldoNoOculteLasCanciones(bool errorPostgres)
    {
        var saldo = new Mock<ICupoPuntosNostalgiaServicio>(MockBehavior.Strict);
        Exception error = errorPostgres
            ? new PostgresException("Tabla no disponible", "ERROR", "ERROR", "42P01")
            : new TimeoutException("Tiempo de espera agotado");
        saldo.Setup(s => s.ObtenerSaldo(21, default)).ThrowsAsync(error);
        var logger = new Mock<ILogger<RankingMusicaServicio>>();
        var servicio = CrearServicio(saldo.Object, logger.Object);

        var ronda = await servicio.ObtenerActual(21);

        Assert.NotNull(ronda);
        Assert.Null(ronda.Saldo);
        var cancion = Assert.Single(Assert.Single(ronda.Decadas).Candidaturas);
        Assert.Equal("Canción", cancion.Titulo);
        Assert.Equal(8, cancion.Puntos);
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            error, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task QueLaConsultaConSesionConserveElSaldoCuandoEstaDisponible()
    {
        var saldo = new Mock<ICupoPuntosNostalgiaServicio>();
        var esperado = new SaldoDiarioPuntosNostalgiaDto(5, 3, DateTimeOffset.UtcNow.AddDays(1), 15);
        saldo.Setup(s => s.ObtenerSaldo(21, default)).ReturnsAsync(esperado);

        var ronda = await CrearServicio(saldo.Object).ObtenerActual(21);

        Assert.Equal(esperado, ronda!.Saldo);
    }

    [Fact]
    public async Task QueLosVisitantesNoConsultenElSaldo()
    {
        var saldo = new Mock<ICupoPuntosNostalgiaServicio>(MockBehavior.Strict);
        var ronda = await CrearServicio(saldo.Object).ObtenerActual();

        Assert.NotNull(ronda);
        Assert.Null(ronda.Saldo);
        saldo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueLosErroresDeProgramacionNoSeOculten()
    {
        var saldo = new Mock<ICupoPuntosNostalgiaServicio>();
        saldo.Setup(s => s.ObtenerSaldo(21, default)).ThrowsAsync(new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => CrearServicio(saldo.Object).ObtenerActual(21));
    }

    [Fact]
    public async Task QueLaCancelacionNoSeConviertaEnUnaRespuestaExitosa()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var saldo = new Mock<ICupoPuntosNostalgiaServicio>();
        saldo.Setup(s => s.ObtenerSaldo(21, cts.Token)).ThrowsAsync(new OperationCanceledException(cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => CrearServicio(saldo.Object).ObtenerActual(21, cts.Token));
    }

    private static RankingMusicaServicio CrearServicio(ICupoPuntosNostalgiaServicio saldo,
        ILogger<RankingMusicaServicio>? logger = null)
    {
        var ahora = DateTimeOffset.UtcNow;
        var repositorio = new Mock<IRankingMusicaRepositorio>(MockBehavior.Strict);
        repositorio.Setup(r => r.ObtenerRondaAbierta(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RankingMusica { Apertura = ahora.AddDays(-1), Cierre = ahora.AddDays(1) });
        var cancion = new CandidaturaRankingMusica(0, 21, new Decada(1980), "Canción", "Artista", ahora);
        repositorio.Setup(r => r.ListarCandidaturas(0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CandidaturaRankingMusicaConPuntos(cancion, 8) });
        return new(repositorio.Object, Mock.Of<ICategoriaServicio>(), saldo, TimeProvider.System,
            Options.Create(new RankingMusicaOptions()), logger);
    }
}
