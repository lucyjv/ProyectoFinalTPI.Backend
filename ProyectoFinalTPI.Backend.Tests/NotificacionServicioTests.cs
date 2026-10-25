using Moq;
using ProyectoFinalTPI.Backend.Dtos.Notificacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class NotificacionServicioTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (
        Mock<INotificacionRepositorio> repoMock,
        Mock<IUsuarioRepositorio> usuarioRepoMock,
        Mock<INotificacionPush> pushMock,
        NotificacionServicio servicio)
        CrearServicio()
    {
        var repoMock = new Mock<INotificacionRepositorio>();
        var usuarioRepoMock = new Mock<IUsuarioRepositorio>();
        var pushMock = new Mock<INotificacionPush>();

        pushMock
            .Setup(p => p.EnviarAsync(
                It.IsAny<int>(),
                It.IsAny<NotificacionDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var servicio = new NotificacionServicio(
            repoMock.Object,
            usuarioRepoMock.Object,
            pushMock.Object);

        return (repoMock, usuarioRepoMock, pushMock, servicio);
    }

    // ── NotificarNuevoSeguidorAsync ───────────────────────────────────────────

    [Fact]
    public async Task NotificarNuevoSeguidor_guarda_notificacion_con_datos_correctos()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        await servicio.NotificarNuevoSeguidorAsync(
            usuarioDestinatarioId: 10,
            nuevoSeguidorId: 20);

        repoMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n =>
                n.UsuarioDestinatarioId == 10 &&
                n.EmisorId == 20 &&
                n.Tipo == TipoNotificacion.NuevoSeguidor &&
                !n.Leida),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotificarNuevoSeguidor_no_consulta_unicidad_porque_cada_seguidor_es_evento_distinto()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        await servicio.NotificarNuevoSeguidorAsync(10, 20);

        repoMock.Verify(r => r.ExisteNotificacionPendienteAsync(
            It.IsAny<int>(),
            It.IsAny<TipoNotificacion>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotificarNuevoSeguidor_envia_push_al_destinatario()
    {
        var (_, _, pushMock, servicio) = CrearServicio();

        await servicio.NotificarNuevoSeguidorAsync(
            usuarioDestinatarioId: 10,
            nuevoSeguidorId: 20);

        pushMock.Verify(p => p.EnviarAsync(
            10,
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── NotificarComentarioEnPublicacionAsync ────────────────────────────────

    [Fact]
    public async Task NotificarComentario_guarda_notificacion_cuando_no_existe_una_pendiente()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.ComentarioEnPublicacion, 99,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await servicio.NotificarComentarioEnPublicacionAsync(
            usuarioDestinatarioId: 5,
            publicacionId: 99,
            comentadorId: 7);

        repoMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n =>
                n.UsuarioDestinatarioId == 5 &&
                n.PublicacionId == 99 &&
                n.EmisorId == 7 &&
                n.Tipo == TipoNotificacion.ComentarioEnPublicacion),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotificarComentario_no_guarda_cuando_ya_existe_una_pendiente()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.ComentarioEnPublicacion, 99,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await servicio.NotificarComentarioEnPublicacionAsync(5, 99, 7);

        repoMock.Verify(r => r.GuardarAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotificarComentario_no_envia_push_cuando_ya_existe_una_pendiente()
    {
        var (repoMock, _, pushMock, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                It.IsAny<int>(),
                TipoNotificacion.ComentarioEnPublicacion,
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await servicio.NotificarComentarioEnPublicacionAsync(5, 99, 7);

        pushMock.Verify(p => p.EnviarAsync(
            It.IsAny<int>(),
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── NotificarPuntuacionEnPublicacionAsync ────────────────────────────────

    [Fact]
    public async Task NotificarPuntuacion_guarda_notificacion_cuando_no_existe_una_pendiente()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                3, TipoNotificacion.PuntuacionEnPublicacion, 55,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await servicio.NotificarPuntuacionEnPublicacionAsync(
            usuarioDestinatarioId: 3,
            publicacionId: 55,
            puntuadorId: 8);

        repoMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n =>
                n.UsuarioDestinatarioId == 3 &&
                n.PublicacionId == 55 &&
                n.EmisorId == 8 &&
                n.Tipo == TipoNotificacion.PuntuacionEnPublicacion),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotificarPuntuacion_no_guarda_cuando_ya_existe_una_pendiente()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                It.IsAny<int>(),
                TipoNotificacion.PuntuacionEnPublicacion,
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await servicio.NotificarPuntuacionEnPublicacionAsync(3, 55, 8);

        repoMock.Verify(r => r.GuardarAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── NotificarMensajeChatAsync ────────────────────────────────────────────

    [Fact]
    public async Task NotificarMensajeChat_guarda_notificacion_cuando_no_existe_una_pendiente()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                10, TipoNotificacion.MensajeChat, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await servicio.NotificarMensajeChatAsync(
            usuarioDestinatarioId: 10,
            emisorId: 20);

        repoMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n =>
                n.UsuarioDestinatarioId == 10 &&
                n.EmisorId == 20 &&
                n.Tipo == TipoNotificacion.MensajeChat &&
                n.PublicacionId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotificarMensajeChat_no_guarda_cuando_ya_existe_una_pendiente()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                It.IsAny<int>(),
                TipoNotificacion.MensajeChat,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await servicio.NotificarMensajeChatAsync(10, 20);

        repoMock.Verify(r => r.GuardarAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── NotificarMensajesCorreoAsync ─────────────────────────────────────────

    [Fact]
    public async Task NotificarMensajesCorreo_guarda_notificacion_cuando_no_existe_una_pendiente()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                10, TipoNotificacion.MensajesCorreo, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await servicio.NotificarMensajesCorreoAsync(usuarioDestinatarioId: 10);

        repoMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n =>
                n.UsuarioDestinatarioId == 10 &&
                n.Tipo == TipoNotificacion.MensajesCorreo &&
                n.PublicacionId == null &&
                n.EmisorId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotificarMensajesCorreo_no_guarda_cuando_ya_existe_una_pendiente()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                It.IsAny<int>(),
                TipoNotificacion.MensajesCorreo,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await servicio.NotificarMensajesCorreoAsync(10);

        repoMock.Verify(r => r.GuardarAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── ObtenerNotificacionesAsync ────────────────────────────────────────────

    [Fact]
    public async Task ObtenerNotificaciones_devuelve_dtos_mapeados_correctamente()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        var emisorFalso = new Usuario { Id = 5, Username = "seguidor_test" };
        var notificacionesFalsas = new List<Notificacion>
        {
            new()
            {
                Id = 1,
                UsuarioDestinatarioId = 10,
                Tipo = TipoNotificacion.NuevoSeguidor,
                Leida = false,
                FechaCreacion = DateTime.UtcNow.AddMinutes(-10),
                EmisorId = 5,
                Emisor = emisorFalso
            },
            new()
            {
                Id = 2,
                UsuarioDestinatarioId = 10,
                Tipo = TipoNotificacion.ComentarioEnPublicacion,
                Leida = true,
                FechaCreacion = DateTime.UtcNow.AddMinutes(-5),
                PublicacionId = 99,
                EmisorId = 7,
                Emisor = new Usuario { Id = 7, Username = "comentador_test" }
            }
        };

        repoMock
            .Setup(r => r.ObtenerPorUsuarioAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notificacionesFalsas);

        var resultado = (await servicio.ObtenerNotificacionesAsync(10)).ToList();

        Assert.Equal(2, resultado.Count);
        Assert.Equal(1, resultado[0].Id);
        Assert.Equal(TipoNotificacion.NuevoSeguidor, resultado[0].Tipo);
        Assert.False(resultado[0].Leida);
        Assert.Equal(5, resultado[0].EmisorId);
        Assert.Equal("seguidor_test", resultado[0].EmisorUsername);
        Assert.Equal(2, resultado[1].Id);
        Assert.Equal(TipoNotificacion.ComentarioEnPublicacion, resultado[1].Tipo);
        Assert.True(resultado[1].Leida);
        Assert.Equal(99, resultado[1].PublicacionId);
    }

    [Fact]
    public async Task ObtenerNotificaciones_repositorio_vacio_devuelve_lista_vacia()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ObtenerPorUsuarioAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notificacion>());

        var resultado = await servicio.ObtenerNotificacionesAsync(10);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ObtenerNotificaciones_emisor_null_usa_null_para_username()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        repoMock
            .Setup(r => r.ObtenerPorUsuarioAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notificacion>
            {
                new()
                {
                    Id = 1,
                    UsuarioDestinatarioId = 10,
                    Tipo = TipoNotificacion.AscensoCategoria,
                    Leida = false,
                    FechaCreacion = DateTime.UtcNow,
                    Emisor = null
                }
            });

        var resultado = (await servicio.ObtenerNotificacionesAsync(10)).ToList();

        Assert.Null(resultado[0].EmisorUsername);
    }

    // ── MarcarComoLeidaAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task MarcarComoLeida_delega_al_repositorio_con_ids_correctos()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        await servicio.MarcarComoLeidaAsync(notificacionId: 42, usuarioId: 10);

        repoMock.Verify(r => r.MarcarComoLeidaAsync(
            42, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── MarcarTodasComoLeidasAsync ───────────────────────────────────────────

    [Fact]
    public async Task MarcarTodasComoLeidas_delega_al_repositorio_con_id_correcto()
    {
        var (repoMock, _, _, servicio) = CrearServicio();

        await servicio.MarcarTodasComoLeidasAsync(usuarioId: 10);

        repoMock.Verify(r => r.MarcarTodasComoLeidasAsync(
            10, It.IsAny<CancellationToken>()), Times.Once);
    }
}
