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
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NotificacionesController CrearController(
        INotificacionServicio servicio, int usuarioId = 1)
    {
        var controller = new NotificacionesController(servicio);

        // Simular usuario autenticado con claim "sub" = usuarioId
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

    // ── ObtenerNotificaciones ────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerNotificaciones_devuelve_200_con_lista()
    {
        var servicioMock = new Mock<INotificacionServicio>();
        servicioMock
            .Setup(s => s.ObtenerNotificacionesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificacionesFalsas());

        var controller = CrearController(servicioMock.Object, usuarioId: 1);

        var resultado = await controller.ObtenerNotificaciones(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var lista = Assert.IsAssignableFrom<IEnumerable<NotificacionDto>>(ok.Value);
        Assert.Equal(2, lista.Count());
    }

    [Fact]
    public async Task ObtenerNotificaciones_llama_al_servicio_con_id_del_usuario_autenticado()
    {
        var servicioMock = new Mock<INotificacionServicio>();
        servicioMock
            .Setup(s => s.ObtenerNotificacionesAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificacionDto>());

        var controller = CrearController(servicioMock.Object, usuarioId: 42);

        await controller.ObtenerNotificaciones(CancellationToken.None);

        servicioMock.Verify(s => s.ObtenerNotificacionesAsync(
            42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObtenerNotificaciones_lista_vacia_devuelve_200()
    {
        var servicioMock = new Mock<INotificacionServicio>();
        servicioMock
            .Setup(s => s.ObtenerNotificacionesAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificacionDto>());

        var controller = CrearController(servicioMock.Object);

        var resultado = await controller.ObtenerNotificaciones(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var lista = Assert.IsAssignableFrom<IEnumerable<NotificacionDto>>(ok.Value);
        Assert.Empty(lista);
    }

    // ── MarcarComoLeida ──────────────────────────────────────────────────────

    [Fact]
    public async Task MarcarComoLeida_devuelve_204_cuando_existe_la_notificacion()
    {
        var servicioMock = new Mock<INotificacionServicio>();
        var controller = CrearController(servicioMock.Object, usuarioId: 1);

        var resultado = await controller.MarcarComoLeida(
            notificacionId: 5, CancellationToken.None);

        Assert.IsType<NoContentResult>(resultado);
    }

    [Fact]
    public async Task MarcarComoLeida_llama_al_servicio_con_ids_correctos()
    {
        var servicioMock = new Mock<INotificacionServicio>();
        var controller = CrearController(servicioMock.Object, usuarioId: 10);

        await controller.MarcarComoLeida(
            notificacionId: 99, CancellationToken.None);

        servicioMock.Verify(s => s.MarcarComoLeidaAsync(
            99, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarcarComoLeida_cuando_servicio_lanza_KeyNotFound_el_middleware_devuelve_404()
    {
        // El middleware ManejoExcepcionesMiddleware convierte KeyNotFoundException → 404.
        // Aquí solo verificamos que el controller NO atrapa la excepción
        // (es decir, la deja propagar para que el middleware la maneje).
        var servicioMock = new Mock<INotificacionServicio>();
        servicioMock
            .Setup(s => s.MarcarComoLeidaAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("La notificación no existe."));

        var controller = CrearController(servicioMock.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => controller.MarcarComoLeida(999, CancellationToken.None));
    }

    // ── MarcarTodasComoLeidas ────────────────────────────────────────────────

    [Fact]
    public async Task MarcarTodasComoLeidas_devuelve_204()
    {
        var servicioMock = new Mock<INotificacionServicio>();
        var controller = CrearController(servicioMock.Object, usuarioId: 1);

        var resultado = await controller.MarcarTodasComoLeidas(CancellationToken.None);

        Assert.IsType<NoContentResult>(resultado);
    }

    [Fact]
    public async Task MarcarTodasComoLeidas_llama_al_servicio_con_id_del_usuario_autenticado()
    {
        var servicioMock = new Mock<INotificacionServicio>();
        var controller = CrearController(servicioMock.Object, usuarioId: 7);

        await controller.MarcarTodasComoLeidas(CancellationToken.None);

        servicioMock.Verify(s => s.MarcarTodasComoLeidasAsync(
            7, It.IsAny<CancellationToken>()), Times.Once);
    }
}
