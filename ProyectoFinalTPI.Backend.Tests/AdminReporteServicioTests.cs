using Moq;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminReporteServicioTests
{
    private readonly Mock<IReporteRepositorio> _repositorio = new();
    private readonly Mock<IAdminPublicacionServicio> _publicaciones = new();
    private readonly Mock<IAdminComentarioServicio> _comentarios = new();
    private readonly Mock<IAdminUsuarioServicio> _usuarios = new();
    private readonly Mock<INotificacionServicio> _notificaciones = new();

    [Fact]
    public async Task QueDesestimarNoModereElContenido()
    {
        var reporte = new Reporte { Id = 3, TipoContenido = TipoContenidoReporte.Publicacion };
        _repositorio.Setup(r => r.Obtener(3, It.IsAny<CancellationToken>())).ReturnsAsync(reporte);

        await Servicio().Resolver(1, new ResolverReporteAdminDto
        {
            ReporteId = 3,
            Accion = AccionReporte.Desestimar
        });

        _publicaciones.VerifyNoOtherCalls();
        _comentarios.VerifyNoOtherCalls();
        _repositorio.Verify(r => r.ResolverRelacionados(
            reporte, 1, AccionReporte.Desestimar, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueEliminarComentarioUseModeracionExistente()
    {
        var reporte = new Reporte
        {
            Id = 4,
            TipoContenido = TipoContenidoReporte.Comentario,
            ContenidoId = 12,
            UsuarioReportadoId = 8
        };
        _repositorio.Setup(r => r.Obtener(4, It.IsAny<CancellationToken>())).ReturnsAsync(reporte);

        await Servicio().Resolver(1, new ResolverReporteAdminDto
        {
            ReporteId = 4,
            Accion = AccionReporte.EliminarContenido
        });

        _comentarios.Verify(c => c.Eliminar(12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueSuspenderUseUsuarioReportadoYNotifique()
    {
        var reporte = new Reporte { Id = 5, UsuarioReportadoId = 9 };
        _repositorio.Setup(r => r.Obtener(5, It.IsAny<CancellationToken>())).ReturnsAsync(reporte);

        await Servicio().Resolver(1, new ResolverReporteAdminDto
        {
            ReporteId = 5,
            Accion = AccionReporte.SuspenderUsuario,
            DiasSuspension = 15
        });

        _usuarios.Verify(u => u.CambiarSuspension(9, 1,
            It.Is<CambiarSuspensionUsuarioDto>(d => d.Suspender && d.Dias == 15),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificaciones.Verify(n => n.NotificarAdministrativaAsync(
            9, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    private AdminReporteServicio Servicio() => new(
        _repositorio.Object, _publicaciones.Object, _comentarios.Object,
        _usuarios.Object, _notificaciones.Object);
}
