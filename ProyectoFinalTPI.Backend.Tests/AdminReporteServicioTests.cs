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
    public async Task QueListarConserveLosFiltrosYRechaceValoresInvalidos()
    {
        var filtro = new FiltroReportesAdminDto
        {
            Pagina = 1, Tamanio = 100, TipoContenido = TipoContenidoReporte.Publicacion,
            Estado = EstadoReporte.Pendiente, Motivo = MotivoReporte.Spam
        };
        var pagina = new PaginaAdminDto<ReporteAdminDto>();
        _repositorio.Setup(r => r.Listar(filtro, default)).ReturnsAsync(pagina);

        Assert.Same(pagina, await Servicio().Listar(filtro));
        var filtrosInvalidos = new FiltroReportesAdminDto[]
        {
            new() { Pagina = 0 },
            new() { Tamanio = 101 },
            new() { TipoContenido = (TipoContenidoReporte)999 },
            new() { Motivo = (MotivoReporte)999 },
            new() { Estado = (EstadoReporte)999 }
        };
        foreach (var invalido in filtrosInvalidos)
            await Assert.ThrowsAsync<ArgumentException>(() => Servicio().Listar(invalido));
        _repositorio.Verify(r => r.Listar(filtro, default), Times.Once);
        _repositorio.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, "Contenido reportado que no cumple los estándares de la comunidad.")]
    [InlineData("  Contenido denunciado  ", "Contenido denunciado")]
    public async Task QueOcultarUnaPublicacionUseElMotivoNormalizado(string? observacion, string motivo)
    {
        var reporte = new Reporte { Id = 7, ContenidoId = 21, TipoContenido = TipoContenidoReporte.Publicacion };
        _repositorio.Setup(r => r.Obtener(7, default)).ReturnsAsync(reporte);

        await Servicio().Resolver(1, new() { ReporteId = 7, Accion = AccionReporte.OcultarContenido, Observacion = observacion });

        _publicaciones.Verify(p => p.CambiarVisibilidad(21,
            It.Is<CambiarVisibilidadPublicacionDto>(d => d.Ocultar && d.Motivo == motivo), default), Times.Once);
        _repositorio.Verify(r => r.ResolverRelacionados(reporte, 1, AccionReporte.OcultarContenido, observacion, default), Times.Once);
    }

    [Theory]
    [InlineData(AccionReporte.OcultarContenido)]
    [InlineData(AccionReporte.EliminarContenido)]
    public async Task QueUnPerfilNoSeModereComoContenido(AccionReporte accion)
    {
        _repositorio.Setup(r => r.Obtener(7, default)).ReturnsAsync(
            new Reporte { Id = 7, TipoContenido = TipoContenidoReporte.Perfil });

        await Assert.ThrowsAsync<ArgumentException>(() => Servicio().Resolver(1, new() { ReporteId = 7, Accion = accion }));

        _repositorio.Verify(r => r.Obtener(7, default), Times.Once);
        _repositorio.VerifyNoOtherCalls();
        _publicaciones.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(TipoContenidoReporte.Publicacion)]
    [InlineData(TipoContenidoReporte.MensajeChat)]
    public async Task QueEliminarUseLaOperacionDelTipoDeContenido(TipoContenidoReporte tipo)
    {
        var reporte = new Reporte { Id = 7, ContenidoId = 21, UsuarioReportadoId = 9, TipoContenido = tipo };
        _repositorio.Setup(r => r.Obtener(7, default)).ReturnsAsync(reporte);

        await Servicio().Resolver(1, new() { ReporteId = 7, Accion = AccionReporte.EliminarContenido });

        if (tipo == TipoContenidoReporte.Publicacion)
        {
            _publicaciones.Verify(p => p.Eliminar(21, default), Times.Once);
            _notificaciones.VerifyNoOtherCalls();
        }
        else
        {
            _repositorio.Verify(r => r.EliminarMensajeChat(21, default), Times.Once);
            _notificaciones.Verify(n => n.NotificarAdministrativaAsync(9, null, default), Times.Once);
        }
    }

    [Fact]
    public async Task QueDarDeBajaActueSobreElUsuarioReportadoYNotifiqueElContexto()
    {
        var reporte = new Reporte { Id = 7, UsuarioReportadoId = 9, ContextoId = 21 };
        _repositorio.Setup(r => r.Obtener(7, default)).ReturnsAsync(reporte);

        await Servicio().Resolver(1, new() { ReporteId = 7, Accion = AccionReporte.DarDeBajaUsuario });

        _usuarios.Verify(u => u.DarDeBaja(9, 1, default), Times.Once);
        _notificaciones.Verify(n => n.NotificarAdministrativaAsync(9, 21, default), Times.Once);
    }

    [Fact]
    public async Task QueElReporteYaResueltoNoVuelvaAModerarse()
    {
        _repositorio.Setup(r => r.Obtener(7, default)).ReturnsAsync(
            new Reporte { Id = 7, Estado = EstadoReporte.Resuelto });

        await Assert.ThrowsAsync<ArgumentException>(() => Servicio().Resolver(1,
            new() { ReporteId = 7, Accion = AccionReporte.EliminarContenido }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Servicio().Resolver(1,
            new() { ReporteId = 99, Accion = AccionReporte.EliminarContenido }));
    }

    [Theory]
    [InlineData(0, AccionReporte.Desestimar, 0, 0)]
    [InlineData(7, (AccionReporte)999, 0, 0)]
    [InlineData(7, AccionReporte.SuspenderUsuario, 366, 0)]
    [InlineData(7, AccionReporte.Desestimar, 0, 1001)]
    public async Task QueResolucionInvalidaNoConsulteElReporte(int id, AccionReporte accion, int dias, int caracteres)
    {
        var dto = new ResolverReporteAdminDto { ReporteId = id, Accion = accion, DiasSuspension = dias, Observacion = new string('x', caracteres) };

        await Assert.ThrowsAsync<ArgumentException>(() => Servicio().Resolver(1, dto));

        _repositorio.VerifyNoOtherCalls();
    }

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
