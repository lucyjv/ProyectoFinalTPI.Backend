using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Services;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class RankingMusicaWorkerTests
{
    [Fact]
    public async Task QueProceseRondasVencidasYEspereHastaElSiguienteCierre()
    {
        // Arrange
        var ahora = new DateTimeOffset(2026, 11, 21, 3, 0, 0, TimeSpan.Zero);
        var cierreVigente = ahora.AddDays(14);
        var cierreSiguiente = cierreVigente.AddDays(15);
        var cierre = new Mock<IRankingMusicaServicio>(MockBehavior.Strict);
        cierre.SetupSequence(servicio => servicio.ProcesarRonda(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ahora.AddDays(-1))
            .ReturnsAsync(cierreVigente)
            .ReturnsAsync(cierreSiguiente);
        var primeraEspera = new TaskCompletionSource<(TimerCallback Callback, object? Estado, TimeSpan Demora)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var segundaEspera = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reloj = new Mock<TimeProvider>(MockBehavior.Strict);
        reloj.Setup(reloj => reloj.GetUtcNow()).Returns(() => ahora);
        reloj.Setup(reloj => reloj.CreateTimer(
                It.IsAny<TimerCallback>(), It.IsAny<object?>(), It.IsAny<TimeSpan>(), Timeout.InfiniteTimeSpan))
            .Returns((TimerCallback callback, object? estado, TimeSpan demora, TimeSpan _) =>
            {
                if (!primeraEspera.TrySetResult((callback, estado, demora)))
                    segundaEspera.SetResult(demora);
                return Mock.Of<ITimer>();
            });
        await using var servicios = new ServiceCollection()
            .AddScoped<IRankingMusicaServicio>(_ => cierre.Object)
            .BuildServiceProvider();
        using var worker = new RankingMusicaWorker(servicios.GetRequiredService<IServiceScopeFactory>(),
            reloj.Object, NullLogger<RankingMusicaWorker>.Instance);

        // Act
        await worker.StartAsync(default);
        try
        {
            var espera = await primeraEspera.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(TimeSpan.FromDays(14), espera.Demora);
            cierre.Verify(servicio => servicio.ProcesarRonda(It.IsAny<CancellationToken>()), Times.Exactly(2));

            ahora = cierreVigente;
            espera.Callback(espera.Estado);
            var demoraSiguiente = await segundaEspera.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            Assert.Equal(TimeSpan.FromDays(15), demoraSiguiente);
            cierre.Verify(servicio => servicio.ProcesarRonda(It.IsAny<CancellationToken>()), Times.Exactly(3));
        }
        finally
        {
            await worker.StopAsync(default);
        }
    }
}
