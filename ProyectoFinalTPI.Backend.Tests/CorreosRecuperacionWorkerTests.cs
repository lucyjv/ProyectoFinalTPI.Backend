using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class CorreosRecuperacionWorkerTests
{
    [Fact]
    public async Task OmiteUsuariosSinCorreoYContinuaProcesandoDespuesDeUnError()
    {
        var entregas = Channel.CreateUnbounded<CorreoElectronicoDto>();
        var correo = new CorreoElectronicoDto("usuario@example.test", "Recuperación", "", "", "solicitud-1");
        var procesador = new Mock<IProcesadorRecuperacionContrasena>(MockBehavior.Strict);
        procesador.Setup(p => p.PrepararRecuperacionAsync("omitido@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CorreoElectronicoDto?)null);
        procesador.Setup(p => p.PrepararRecuperacionAsync(correo.Destinatario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correo);
        var envio = new Mock<IEnvioCorreo>(MockBehavior.Strict);
        envio.Setup(e => e.EnviarAsync(correo, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EnvioCorreoException("proveedor"));
        envio.Setup(e => e.EnviarAsync(It.Is<CorreoElectronicoDto>(c => c.Destinatario == "aviso@example.test"),
                It.IsAny<CancellationToken>()))
            .Returns((CorreoElectronicoDto mensaje, CancellationToken ct) => entregas.Writer.WriteAsync(mensaje, ct).AsTask());
        await using var servicios = new ServiceCollection().AddSingleton(procesador.Object).AddSingleton(envio.Object)
            .BuildServiceProvider();
        using var worker = CrearWorker(servicios);
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Assert.True(worker.EncolarSolicitud("omitido@example.test"));
        Assert.True(worker.EncolarSolicitud(correo.Destinatario));
        Assert.True(worker.EncolarSolicitud(correo.Destinatario));
        Assert.True(worker.EncolarAviso("aviso@example.test", Guid.NewGuid()));

        await worker.StartAsync(limite.Token);
        try
        {
            var aviso = await entregas.Reader.ReadAsync(limite.Token);
            Assert.Equal("aviso@example.test", aviso.Destinatario);
            procesador.Verify(p => p.PrepararRecuperacionAsync(correo.Destinatario, It.IsAny<CancellationToken>()), Times.Once);
            envio.Verify(e => e.EnviarAsync(correo, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void UnaSolicitudDuplicadaNoOcupaOtroLugarEnUnaColaLlena()
    {
        using var servicios = new ServiceCollection().BuildServiceProvider();
        using var worker = CrearWorker(servicios);
        for (var i = 0; i < 128; i++)
            Assert.True(worker.EncolarSolicitud($"usuario{i}@example.test"));

        Assert.True(worker.EncolarSolicitud("usuario0@example.test"));
        Assert.False(worker.EncolarSolicitud("nuevo@example.test"));
    }

    private static CorreosRecuperacionWorker CrearWorker(ServiceProvider servicios) =>
        new(servicios.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new RecuperacionContrasenaOptions { Habilitado = true }),
            NullLogger<CorreosRecuperacionWorker>.Instance);
}
