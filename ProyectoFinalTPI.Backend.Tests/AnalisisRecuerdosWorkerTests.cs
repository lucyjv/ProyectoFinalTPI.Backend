using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Services;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AnalisisRecuerdosWorkerTests
{
    #region Creación y Liberación de Scopes

    [Fact]
    public async Task QueCadaCicloCreeYLibereSuPropioScope()
    {
        //Arrange
        var mocks = new List<Mock<IAnalisisRecuerdosServicio>>();
        var tokensRecibidos = new List<CancellationToken>();
        var disposables = new List<Mock<IDisposable>>();

        var servicios = new ServiceCollection();
        servicios.AddScoped<IAnalisisRecuerdosServicio>(_ =>
        {
            var mock = new Mock<IAnalisisRecuerdosServicio>();
            var disp = mock.As<IDisposable>();

            mock.Setup(m => m.ProcesarUno(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(ct => tokensRecibidos.Add(ct))
                .ReturnsAsync(false);

            disp.Setup(d => d.Dispose());

            mocks.Add(mock);
            disposables.Add(disp);
            return mock.Object;
        });

        await using var provider = servicios.BuildServiceProvider();
        using var worker = CrearWorker(provider.GetRequiredService<IServiceScopeFactory>(), habilitado: true);
        using var cancelacion = new CancellationTokenSource();

        //Act
        await worker.EjecutarCiclo(cancelacion.Token);
        await worker.EjecutarCiclo(cancelacion.Token);

        //Assert
        Assert.Equal(2, mocks.Count);
        Assert.NotSame(mocks[0].Object, mocks[1].Object);
        Assert.All(tokensRecibidos, t => Assert.Equal(cancelacion.Token, t));
        disposables[0].Verify(d => d.Dispose(), Times.Once);
        disposables[1].Verify(d => d.Dispose(), Times.Once);
    }

    #endregion

    #region Control de Ciclo de Vida del Worker

    [Fact]
    public async Task QueElWorkerDeshabilitadoNoConsumaLaCola()
    {
        //Arrange
        await using var provider = new ServiceCollection().BuildServiceProvider();
        using var worker = CrearWorker(provider.GetRequiredService<IServiceScopeFactory>(), habilitado: false);

        //Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        //Assert
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private static AnalisisRecuerdosWorker CrearWorker(IServiceScopeFactory scopeFactory, bool habilitado)
    {
        return new AnalisisRecuerdosWorker(
            scopeFactory,
            Options.Create(new OpcionesAnalisisRecuerdos { Habilitado = habilitado }),
            TimeProvider.System,
            NullLogger<AnalisisRecuerdosWorker>.Instance);
    }

    #endregion
}
