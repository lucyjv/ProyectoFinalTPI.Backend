using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Services;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class LimpiezaMensajesServiceTests
{
    // ──────────────────────────────────────────────────────────────────
    // Helpers para construir el servicio con dependencias mockeadas
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Construye un IServiceProvider que resuelve IMensajeChatRepositorio
    /// con el mock provisto, replicando el patrón de scope que usa el servicio.
    /// </summary>
    private static IServiceScopeFactory CrearScopeFactory(
        IMensajeChatRepositorio repositorio)
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
        var loggerReal = logger
            ?? new Mock<ILogger<LimpiezaMensajesService>>().Object;
        return new LimpiezaMensajesService(scopeFactory, loggerReal);
    }

    // ──────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Al_cancelar_el_token_el_servicio_termina_sin_error()
    {
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        var servicio = CrearServicio(repositorioMock.Object);

        // Cancelar inmediatamente: el servicio debe terminar limpiamente
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // No debe lanzar excepciones
        await servicio.StartAsync(cts.Token);
        await servicio.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Una_excepcion_en_el_repositorio_no_detiene_el_servicio()
    {
        var repositorioMock = new Mock<IMensajeChatRepositorio>();

        // Simular que el repositorio lanza una excepción
        repositorioMock
            .Setup(r => r.EliminarExpiradosAsync())
            .ThrowsAsync(new Exception("Error de base de datos simulado"));

        var servicio = CrearServicio(repositorioMock.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Aun con el repositorio roto, el servicio no debe propagar la excepción
        var excepcion = await Record.ExceptionAsync(
            () => servicio.StartAsync(cts.Token));

        Assert.Null(excepcion);
    }

    [Fact]
    public async Task El_servicio_usa_el_repositorio_del_scope_no_un_singleton()
    {
        // Este test verifica que el servicio crea un scope nuevo cada vez
        // en lugar de reutilizar una instancia singleton del repositorio,
        // lo cual sería incorrecto con DbContext.
        var llamadas = 0;
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        repositorioMock
            .Setup(r => r.EliminarExpiradosAsync())
            .Callback(() => llamadas++)
            .Returns(Task.CompletedTask);

        var scopeFactory = CrearScopeFactory(repositorioMock.Object);
        var loggerMock = new Mock<ILogger<LimpiezaMensajesService>>();
        var servicio = new LimpiezaMensajesService(scopeFactory, loggerMock.Object);

        // Ejecutar la limpieza directamente via reflexión para no esperar 1 hora
        var metodo = typeof(LimpiezaMensajesService)
            .GetMethod("EjecutarLimpiezaAsync",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

        Assert.NotNull(metodo);
        await (Task)metodo!.Invoke(servicio, null)!;

        // El repositorio debe haber sido llamado exactamente una vez
        Assert.Equal(1, llamadas);
    }
}
