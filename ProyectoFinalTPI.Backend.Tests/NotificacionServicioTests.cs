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
    private readonly Mock<INotificacionRepositorio> _repositorioMock = new();
    private readonly Mock<IUsuarioRepositorio> _usuarioRepositorioMock = new();
    private readonly Mock<INotificacionPush> _pushMock = new();
    private readonly NotificacionServicio _servicio;

    public NotificacionServicioTests()
    {
        _pushMock
            .Setup(p => p.EnviarAsync(
                It.IsAny<int>(),
                It.IsAny<NotificacionDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _servicio = new NotificacionServicio(
            _repositorioMock.Object,
            _usuarioRepositorioMock.Object,
            _pushMock.Object);
    }

    #region Notificar Nuevo Seguidor

    [Fact]
    public async Task QueNotificarNuevoSeguidorGuardeNotificacionConDatosCorrectos()
    {
        //Arrange
        var destinatarioId = 10;
        var nuevoSeguidorId = 20;

        //Act
        await _servicio.NotificarNuevoSeguidorAsync(
            usuarioDestinatarioId: destinatarioId,
            nuevoSeguidorId: nuevoSeguidorId);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n =>
                n.UsuarioDestinatarioId == 10 &&
                n.EmisorId == 20 &&
                n.Tipo == TipoNotificacion.NuevoSeguidor &&
                !n.Leida),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNotificarNuevoSeguidorNoConsulteUnicidadPorqueCadaSeguidorEsEventoDistinto()
    {
        //Arrange
        var destinatarioId = 10;
        var nuevoSeguidorId = 20;

        //Act
        await _servicio.NotificarNuevoSeguidorAsync(destinatarioId, nuevoSeguidorId);

        //Assert
        _repositorioMock.Verify(r => r.ExisteNotificacionPendienteAsync(
            It.IsAny<int>(),
            It.IsAny<TipoNotificacion>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNotificarNuevoSeguidorEnviePushAlDestinatario()
    {
        //Arrange
        var destinatarioId = 10;
        var nuevoSeguidorId = 20;

        //Act
        await _servicio.NotificarNuevoSeguidorAsync(
            usuarioDestinatarioId: destinatarioId,
            nuevoSeguidorId: nuevoSeguidorId);

        //Assert
        _pushMock.Verify(p => p.EnviarAsync(
            10,
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Notificar Comentario en Publicación

    [Fact]
    public async Task QueNotificarComentarioGuardeNotificacionCuandoNoExisteUnaPendiente()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.ComentarioEnPublicacion, 99,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        //Act
        await _servicio.NotificarComentarioEnPublicacionAsync(
            usuarioDestinatarioId: 5,
            publicacionId: 99,
            comentadorId: 7);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n =>
                n.UsuarioDestinatarioId == 5 &&
                n.EmisorId == 7 &&
                n.PublicacionId == 99 &&
                n.Tipo == TipoNotificacion.ComentarioEnPublicacion &&
                !n.Leida),
            It.IsAny<CancellationToken>()), Times.Once);

        _pushMock.Verify(p => p.EnviarAsync(
            5,
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNotificarComentarioNoGuardeCuandoYaExisteUnaPendiente()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.ComentarioEnPublicacion, 99,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        //Act
        await _servicio.NotificarComentarioEnPublicacionAsync(
            usuarioDestinatarioId: 5,
            publicacionId: 99,
            comentadorId: 7);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNotificarComentarioNoEnviePushCuandoYaExisteUnaPendiente()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.ComentarioEnPublicacion, 99,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        //Act
        await _servicio.NotificarComentarioEnPublicacionAsync(
            usuarioDestinatarioId: 5,
            publicacionId: 99,
            comentadorId: 7);

        //Assert
        _pushMock.Verify(p => p.EnviarAsync(
            It.IsAny<int>(),
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Notificar Puntuación en Publicación

    [Fact]
    public async Task QueNotificarPuntuacionGuardeNotificacionCuandoNoExisteUnaPendiente()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.PuntuacionEnPublicacion, 99,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        //Act
        await _servicio.NotificarPuntuacionEnPublicacionAsync(
            usuarioDestinatarioId: 5,
            publicacionId: 99,
            puntuadorId: 7);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n =>
                n.UsuarioDestinatarioId == 5 &&
                n.EmisorId == 7 &&
                n.PublicacionId == 99 &&
                n.Tipo == TipoNotificacion.PuntuacionEnPublicacion &&
                !n.Leida),
            It.IsAny<CancellationToken>()), Times.Once);

        _pushMock.Verify(p => p.EnviarAsync(
            5,
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNotificarPuntuacionNoGuardeCuandoYaExisteUnaPendiente()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.PuntuacionEnPublicacion, 99,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        //Act
        await _servicio.NotificarPuntuacionEnPublicacionAsync(
            usuarioDestinatarioId: 5,
            publicacionId: 99,
            puntuadorId: 7);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _pushMock.Verify(p => p.EnviarAsync(
            It.IsAny<int>(),
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Notificar Mensaje de Chat

    [Fact]
    public async Task QueNotificarMensajeChatGuardeNotificacionCuandoNoExisteUnaPendiente()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.MensajeChat, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        //Act
        await _servicio.NotificarMensajeChatAsync(
            usuarioDestinatarioId: 5,
            emisorId: 7);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n =>
                n.UsuarioDestinatarioId == 5 &&
                n.EmisorId == 7 &&
                n.Tipo == TipoNotificacion.MensajeChat &&
                !n.Leida),
            It.IsAny<CancellationToken>()), Times.Once);

        _pushMock.Verify(p => p.EnviarAsync(
            5,
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNotificarMensajeChatNoGuardeCuandoYaExisteUnaPendiente()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.MensajeChat, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        //Act
        await _servicio.NotificarMensajeChatAsync(5, 7);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _pushMock.Verify(p => p.EnviarAsync(
            It.IsAny<int>(),
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Notificar Mensajes de Correo

    [Fact]
    public async Task QueNotificarMensajesCorreoGuardeNotificacionCuandoNoExisteUnaPendiente()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.MensajesCorreo, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        //Act
        await _servicio.NotificarMensajesCorreoAsync(usuarioDestinatarioId: 5);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n =>
                n.UsuarioDestinatarioId == 5 &&
                n.Tipo == TipoNotificacion.MensajesCorreo &&
                !n.Leida),
            It.IsAny<CancellationToken>()), Times.Once);

        _pushMock.Verify(p => p.EnviarAsync(
            5,
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNotificarMensajesCorreoNoGuardeCuandoYaExisteUnaPendiente()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExisteNotificacionPendienteAsync(
                5, TipoNotificacion.MensajesCorreo, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        //Act
        await _servicio.NotificarMensajesCorreoAsync(5);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _pushMock.Verify(p => p.EnviarAsync(
            It.IsAny<int>(),
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Notificar Etiquetado En Publicacion

    [Fact]
    public async Task QueNotificarEtiquetadoGuardeNotificacionCuandoNoExisteUnaPendiente()
    {
        //Arrange
        Notificacion? guardada = null;

        _repositorioMock.Setup(r => r.ExisteNotificacionPendienteAsync(
                It.IsAny<int>(),
                TipoNotificacion.EtiquetadoEnPublicacion,
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repositorioMock.Setup(r => r.GuardarAsync(
                It.IsAny<Notificacion>(),
                It.IsAny<CancellationToken>()))
            .Callback<Notificacion, CancellationToken>((n, _) => guardada = n)
            .Returns(Task.CompletedTask);

        //Act
        await _servicio.NotificarEtiquetadoEnPublicacionAsync(
            usuarioDestinatarioId: 10,
            publicacionId: 42,
            emisorId: 5);

        //Assert
        Assert.NotNull(guardada);
        Assert.Equal(10, guardada!.UsuarioDestinatarioId);
        Assert.Equal(TipoNotificacion.EtiquetadoEnPublicacion, guardada.Tipo);
        Assert.Equal(42, guardada.PublicacionId);
        Assert.Equal(5, guardada.EmisorId);
        Assert.False(guardada.Leida);

        _pushMock.Verify(p => p.EnviarAsync(
            10,
            It.Is<NotificacionDto>(dto =>
                dto.Tipo == TipoNotificacion.EtiquetadoEnPublicacion &&
                dto.PublicacionId == 42 &&
                dto.EmisorId == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNotificarEtiquetadoNoGuardeCuandoYaExisteUnaPendiente()
    {
        //Arrange

        _repositorioMock.Setup(r => r.ExisteNotificacionPendienteAsync(
                10,
                TipoNotificacion.EtiquetadoEnPublicacion,
                42,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        //Act
        await _servicio.NotificarEtiquetadoEnPublicacionAsync(
            usuarioDestinatarioId: 10,
            publicacionId: 42,
            emisorId: 5);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _pushMock.Verify(p => p.EnviarAsync(
            It.IsAny<int>(),
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNotificarEtiquetadoNoGuardeSiDestinatarioEsElMismoEmisor()
    {
        //Arrange

        //Act
        await _servicio.NotificarEtiquetadoEnPublicacionAsync(
            usuarioDestinatarioId: 5,
            publicacionId: 42,
            emisorId: 5);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _pushMock.Verify(p => p.EnviarAsync(
            It.IsAny<int>(),
            It.IsAny<NotificacionDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueNotificarPuntuacionGuardePuntajeYLoMapeeADto()
    {
        //Arrange
        _repositorioMock.Setup(r => r.ExisteNotificacionPendienteAsync(
                It.IsAny<int>(), It.IsAny<TipoNotificacion>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Notificacion? guardada = null;
        _repositorioMock.Setup(r => r.GuardarAsync(It.IsAny<Notificacion>(), It.IsAny<CancellationToken>()))
            .Callback<Notificacion, CancellationToken>((n, _) => guardada = n)
            .Returns(Task.CompletedTask);

        //Act
        await _servicio.NotificarPuntuacionEnPublicacionAsync(
            usuarioDestinatarioId: 10,
            publicacionId: 42,
            puntuadorId: 5,
            puntaje: 5);

        //Assert
        Assert.NotNull(guardada);
        Assert.Equal(5, guardada.Puntaje);
        Assert.Equal(TipoNotificacion.PuntuacionEnPublicacion, guardada.Tipo);

        _pushMock.Verify(p => p.EnviarAsync(
            10,
            It.Is<NotificacionDto>(d => d.Puntaje == 5 && d.Tipo == TipoNotificacion.PuntuacionEnPublicacion),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNotificarMeGustaGuardeYEnvieNotificacion()
    {
        //Arrange
        _repositorioMock.Setup(r => r.ExisteNotificacionPendienteAsync(
                It.IsAny<int>(), It.IsAny<TipoNotificacion>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        //Act
        await _servicio.NotificarMeGustaPublicacionAsync(
            usuarioDestinatarioId: 10,
            publicacionId: 42,
            usuarioQueDioLikeId: 7);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n => n.Tipo == TipoNotificacion.MeGustaPublicacion && n.UsuarioDestinatarioId == 10 && n.PublicacionId == 42 && n.EmisorId == 7),
            It.IsAny<CancellationToken>()), Times.Once);

        _pushMock.Verify(p => p.EnviarAsync(
            10,
            It.Is<NotificacionDto>(d => d.Tipo == TipoNotificacion.MeGustaPublicacion && d.PublicacionId == 42),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNotificarCompartirGuardeYEnvieNotificacion()
    {
        //Arrange
        _repositorioMock.Setup(r => r.ExisteNotificacionPendienteAsync(
                It.IsAny<int>(), It.IsAny<TipoNotificacion>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        //Act
        await _servicio.NotificarCompartirPublicacionAsync(
            usuarioDestinatarioId: 10,
            publicacionId: 42,
            reposteadorId: 8);

        //Assert
        _repositorioMock.Verify(r => r.GuardarAsync(
            It.Is<Notificacion>(n => n.Tipo == TipoNotificacion.CompartirPublicacion && n.UsuarioDestinatarioId == 10 && n.PublicacionId == 42 && n.EmisorId == 8),
            It.IsAny<CancellationToken>()), Times.Once);

        _pushMock.Verify(p => p.EnviarAsync(
            10,
            It.Is<NotificacionDto>(d => d.Tipo == TipoNotificacion.CompartirPublicacion && d.PublicacionId == 42),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Obtener Notificaciones

    [Fact]
    public async Task QueObtenerNotificacionesDevuelvaDtosMapeadosCorrectamente()
    {
        //Arrange
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

        _repositorioMock
            .Setup(r => r.ObtenerPorUsuarioAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notificacionesFalsas);

        //Act
        var resultado = (await _servicio.ObtenerNotificacionesAsync(10)).ToList();

        //Assert
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
        _repositorioMock.Verify(r => r.ObtenerPorUsuarioAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueObtenerNotificacionesConRepositorioVacioDevuelvaListaVacia()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ObtenerPorUsuarioAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notificacion>());

        //Act
        var resultado = await _servicio.ObtenerNotificacionesAsync(10);

        //Assert
        Assert.Empty(resultado);
        _repositorioMock.Verify(r => r.ObtenerPorUsuarioAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueObtenerNotificacionesConEmisorNullUseNullParaUsername()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ObtenerPorUsuarioAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
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

        //Act
        var resultado = (await _servicio.ObtenerNotificacionesAsync(10)).ToList();

        //Assert
        Assert.Null(resultado[0].EmisorUsername);
    }

    #endregion

    #region Marcar Como Leídas

    [Fact]
    public async Task QueMarcarComoLeidaDelegueAlRepositorioConIdsCorrectos()
    {
        //Arrange
        var notificacionId = 42;
        var usuarioId = 10;

        //Act
        await _servicio.MarcarComoLeidaAsync(notificacionId: notificacionId, usuarioId: usuarioId);

        //Assert
        _repositorioMock.Verify(r => r.MarcarComoLeidaAsync(
            42, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueMarcarTodasComoLeidasDelegueAlRepositorioConIdCorrecto()
    {
        //Arrange
        var usuarioId = 10;

        //Act
        await _servicio.MarcarTodasComoLeidasAsync(usuarioId: usuarioId);

        //Assert
        _repositorioMock.Verify(r => r.MarcarTodasComoLeidasAsync(
            10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
