using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CoordinadorRankingFeedTests
{
    #region Generaciones Simultáneas y Cancelación

    [Fact]
    public async Task QueSolicitudesSimultaneasCompartanTrabajoYUnaCancelacionNoCanceleALasDemas()
    {
        //Arrange
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generador = new Mock<IGeneradorRankingFeed>();
        var disposable = generador.As<IDisposable>();
        CancellationToken tokenTrabajo = default;
        generador.Setup(g => g.GenerarAsync(7, It.IsAny<CancellationToken>()))
            .Returns(async (int _, CancellationToken ct) =>
            {
                tokenTrabajo = ct;
                inicio.TrySetResult();
                await liberar.Task.WaitAsync(ct);
                return (IReadOnlyList<int>)new[] { 3, 1 };
            });
        disposable.Setup(d => d.Dispose());
        await using var servicios = new ServiceCollection().AddScoped<IGeneradorRankingFeed>(_ => generador.Object).BuildServiceProvider();
        var coordinador = Crear(servicios);
        using var cancelacion = new CancellationTokenSource();

        //Act
        var primera = coordinador.ObtenerAsync(7, cancelacion.Token);
        await inicio.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var segunda = coordinador.ObtenerAsync(7);
        cancelacion.Cancel();
        var error = await Record.ExceptionAsync(() => primera);
        var trabajoCancelado = tokenTrabajo.IsCancellationRequested;
        liberar.TrySetResult();
        var resultado = await segunda;

        //Assert
        Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.False(trabajoCancelado);
        Assert.Equal(new[] { 3, 1 }, resultado);
        generador.Verify(g => g.GenerarAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        disposable.Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public async Task QueUnaRecargaPosteriorGenereOtroRanking()
    {
        //Arrange
        var generador = new Mock<IGeneradorRankingFeed>();
        generador.SetupSequence(g => g.GenerarAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 3 }).ReturnsAsync(new[] { 9 });
        await using var servicios = new ServiceCollection().AddScoped<IGeneradorRankingFeed>(_ => generador.Object).BuildServiceProvider();
        var coordinador = Crear(servicios);

        //Act
        var primera = await coordinador.ObtenerAsync(7);
        var segunda = await coordinador.ObtenerAsync(7);

        //Assert
        Assert.Equal(new[] { 3 }, primera);
        Assert.Equal(new[] { 9 }, segunda);
        generador.Verify(g => g.GenerarAsync(7, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task QueUnFalloNoDejeAlUsuarioConUnTrabajoFallidoGuardado()
    {
        //Arrange
        var generador = new Mock<IGeneradorRankingFeed>();
        generador.SetupSequence(g => g.GenerarAsync(7, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("prueba")).ReturnsAsync(new[] { 9 });
        await using var servicios = new ServiceCollection().AddScoped<IGeneradorRankingFeed>(_ => generador.Object).BuildServiceProvider();
        var coordinador = Crear(servicios);

        //Act
        var error = await Record.ExceptionAsync(() => coordinador.ObtenerAsync(7));
        var resultado = await coordinador.ObtenerAsync(7);

        //Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(new[] { 9 }, resultado);
        generador.Verify(g => g.GenerarAsync(7, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task QueUsuariosDistintosNoCompartanRanking()
    {
        //Arrange
        var generador = new Mock<IGeneradorRankingFeed>();
        generador.Setup(g => g.GenerarAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int usuario, CancellationToken _) => (IReadOnlyList<int>)new[] { usuario });
        await using var servicios = new ServiceCollection().AddScoped<IGeneradorRankingFeed>(_ => generador.Object).BuildServiceProvider();
        var coordinador = Crear(servicios);

        //Act
        var resultados = await Task.WhenAll(coordinador.ObtenerAsync(7), coordinador.ObtenerAsync(8));

        //Assert
        Assert.Equal(new[] { 7 }, resultados[0]);
        Assert.Equal(new[] { 8 }, resultados[1]);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private static CoordinadorRankingFeed Crear(ServiceProvider servicios) => new(
        servicios.GetRequiredService<IServiceScopeFactory>(), Options.Create(new FeedRecomendacionesOptions()),
        Mock.Of<IHostApplicationLifetime>());

    #endregion
}
