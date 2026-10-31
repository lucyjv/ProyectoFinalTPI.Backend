using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Services;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class LimpiezaMensajesServiceTests
{
    #region Ejecución y Manejo de Errores

    [Fact]
    public async Task QueAlCancelarTokenElServicioTermineSinError()
    {
        //Arrange
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        var servicio = CrearServicio(repositorioMock.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        //Act
        await servicio.StartAsync(cts.Token);
        await servicio.StopAsync(CancellationToken.None);

        //Assert
        // Termina limpiamente sin lanzar excepciones
    }

    [Fact]
    public async Task QueExcepcionEnRepositorioNoDetengaElServicio()
    {
        //Arrange
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        repositorioMock
            .Setup(r => r.EliminarExpiradosAsync())
            .ThrowsAsync(new Exception("Error de base de datos simulado"));

        var servicio = CrearServicio(repositorioMock.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        //Act
        var excepcion = await Record.ExceptionAsync(() => servicio.StartAsync(cts.Token));

        //Assert
        Assert.Null(excepcion);
    }

    #endregion

    #region Scopes de Dependencias

    [Fact]
    public async Task QueElServicioUseRepositorioDelScope()
    {
        //Arrange
        var llamadas = 0;
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        repositorioMock
            .Setup(r => r.EliminarExpiradosAsync())
            .Callback(() => llamadas++)
            .Returns(Task.CompletedTask);

        var scopeFactory = CrearScopeFactory(repositorioMock.Object);
        var loggerMock = new Mock<ILogger<LimpiezaMensajesService>>();
        var servicio = new LimpiezaMensajesService(scopeFactory, loggerMock.Object);

        var metodo = typeof(LimpiezaMensajesService)
            .GetMethod("EjecutarLimpiezaAsync",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

        Assert.NotNull(metodo);

        //Act
        await (Task)metodo!.Invoke(servicio, null)!;

        //Assert
        Assert.Equal(1, llamadas);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private static IServiceScopeFactory CrearScopeFactory(IMensajeChatRepositorio repositorio)
    {
        var services = new ServiceCollection();
        services.AddScoped<IMensajeChatRepositorio>(_ => repositorio);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    private static LimpiezaMensajesService CrearServicio(
        IMensajeChatRepositorio repositorio,
        ILogger<LimpiezaMensajesService>? logger = null)
    {
        var scopeFactory = CrearScopeFactory(repositorio);
        var loggerReal = logger ?? new Mock<ILogger<LimpiezaMensajesService>>().Object;
        return new LimpiezaMensajesService(scopeFactory, loggerReal);
    }

    #endregion
}
