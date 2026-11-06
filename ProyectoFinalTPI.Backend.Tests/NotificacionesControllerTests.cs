using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Notificacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class NotificacionesControllerTests
{
    private readonly Mock<INotificacionServicio> _servicioMock = new();

    #region Obtener Notificaciones

    [Fact]
    public async Task QueObtenerNotificacionesDevuelva200ConLista()
    {
        //Arrange
        _servicioMock
            .Setup(s => s.ObtenerNotificacionesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificacionesFalsas());

        var controller = CrearController(usuarioId: 1);

        //Act
        var resultado = await controller.ObtenerNotificaciones(CancellationToken.None);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var lista = Assert.IsAssignableFrom<IEnumerable<NotificacionDto>>(ok.Value);
        Assert.Equal(2, lista.Count());
    }

    [Fact]
    public async Task QueObtenerNotificacionesInvoqueServicioConIdDeUsuarioAutenticado()
    {
        //Arrange
        _servicioMock
            .Setup(s => s.ObtenerNotificacionesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificacionDto>());

        var controller = CrearController(usuarioId: 42);

        //Act
        await controller.ObtenerNotificaciones(CancellationToken.None);

        //Assert
        _servicioMock.Verify(s => s.ObtenerNotificacionesAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueObtenerNotificacionesDevuelva200CuandoLaListaEstaVacia()
    {
        //Arrange
        _servicioMock
            .Setup(s => s.ObtenerNotificacionesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificacionDto>());

        var controller = CrearController();

        //Act
        var resultado = await controller.ObtenerNotificaciones(CancellationToken.None);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var lista = Assert.IsAssignableFrom<IEnumerable<NotificacionDto>>(ok.Value);
        Assert.Empty(lista);
    }

    #endregion

    #region Marcar Como Leída

    [Fact]
    public async Task QueMarcarComoLeidaDevuelva204CuandoExisteNotificacion()
    {
        //Arrange
        var controller = CrearController(usuarioId: 1);

        //Act
        var resultado = await controller.MarcarComoLeida(notificacionId: 5, CancellationToken.None);

        //Assert
        Assert.IsType<NoContentResult>(resultado);
    }

    [Fact]
    public async Task QueMarcarComoLeidaInvoqueServicioConIdsCorrectos()
    {
        //Arrange
        var controller = CrearController(usuarioId: 10);

        //Act
        await controller.MarcarComoLeida(notificacionId: 99, CancellationToken.None);

        //Assert
        _servicioMock.Verify(s => s.MarcarComoLeidaAsync(99, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueMarcarComoLeidaPropagueKeyNotFoundCuandoNoExisteNotificacion()
    {
        //Arrange
        _servicioMock
            .Setup(s => s.MarcarComoLeidaAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("La notificación no existe."));

        var controller = CrearController();

        //Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.MarcarComoLeida(999, CancellationToken.None));
    }

    #endregion

    #region Marcar Todas Como Leídas

    [Fact]
    public async Task QueMarcarTodasComoLeidasDevuelva204()
    {
        //Arrange
        var controller = CrearController(usuarioId: 1);

        //Act
        var resultado = await controller.MarcarTodasComoLeidas(CancellationToken.None);

        //Assert
        Assert.IsType<NoContentResult>(resultado);
    }

    [Fact]
    public async Task QueMarcarTodasComoLeidasInvoqueServicioConIdDeUsuarioAutenticado()
    {
        //Arrange
        var controller = CrearController(usuarioId: 7);

        //Act
        await controller.MarcarTodasComoLeidas(CancellationToken.None);

        //Assert
        _servicioMock.Verify(s => s.MarcarTodasComoLeidasAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private NotificacionesController CrearController(int usuarioId = 1)
    {
        var controller = new NotificacionesController(_servicioMock.Object);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuarioId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    private static List<NotificacionDto> NotificacionesFalsas() =>
    [
        new() { Id = 1, Tipo = TipoNotificacion.NuevoSeguidor, Leida = false },
        new() { Id = 2, Tipo = TipoNotificacion.MensajeChat,   Leida = false }
    ];

    #endregion
}
