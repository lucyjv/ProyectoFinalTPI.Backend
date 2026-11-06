using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminComentarioServicioTests
{
    [Fact]
    public async Task QueEliminarComentarioNotifiqueAlAutor()
    {
        var repositorio = new Mock<IAdminComentarioRepositorio>();
        var notificaciones = new Mock<INotificacionServicio>();
        var comentario = new Comentario { Id = 4, UsuarioId = 7, PublicacionId = 9 };
        repositorio.Setup(r => r.Obtener(4, It.IsAny<CancellationToken>())).ReturnsAsync(comentario);
        await new AdminComentarioServicio(repositorio.Object, notificaciones.Object).Eliminar(4);
        repositorio.Verify(r => r.Eliminar(comentario, It.IsAny<CancellationToken>()), Times.Once);
        notificaciones.Verify(n => n.NotificarAdministrativaAsync(7, 9, It.IsAny<CancellationToken>()), Times.Once);
    }
}
