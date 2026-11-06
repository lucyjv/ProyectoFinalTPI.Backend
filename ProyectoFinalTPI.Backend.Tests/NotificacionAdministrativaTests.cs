using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class NotificacionAdministrativaTests
{
    [Fact]
    public async Task QueNotificacionAdministrativaUseSistemaExistenteYContextoPublicacion()
    {
        var repositorio = new Mock<INotificacionRepositorio>();
        var push = new Mock<INotificacionPush>();
        Notificacion? guardada = null;
        repositorio.Setup(r => r.GuardarAsync(It.IsAny<Notificacion>(), It.IsAny<CancellationToken>()))
            .Callback<Notificacion, CancellationToken>((n, _) => { n.Id = 4; guardada = n; });
        var servicio = new NotificacionServicio(
            repositorio.Object, Mock.Of<IUsuarioRepositorio>(), push.Object);
        await servicio.NotificarAdministrativaAsync(7, 12);
        Assert.NotNull(guardada);
        Assert.Equal(TipoNotificacion.Administrativa, guardada.Tipo);
        Assert.Equal(12, guardada.PublicacionId);
        push.Verify(p => p.EnviarAsync(7,
            It.Is<ProyectoFinalTPI.Backend.Dtos.Notificacion.NotificacionDto>(d =>
                d.Tipo == TipoNotificacion.Administrativa && d.PublicacionId == 12),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
