using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Chat;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ChatControllerTests
{
    private readonly Mock<IChatServicio> _chatServicioMock = new();

    #region Obtener Conversaciones

    [Fact]
    public async Task QueObtenerConversacionesAutenticadoDevuelvaOkConLista()
    {
        //Arrange
        const int usuarioId = 5;
        var conversacionesEsperadas = new List<ConversacionResumenDto>
        {
            new() { OtroUsuarioId = 10, OtroUsuarioUsername = "maria", UltimoMensaje = "Hola!", FechaUltimoMensaje = DateTime.UtcNow }
        };

        _chatServicioMock
            .Setup(s => s.ObtenerConversacionesActivasAsync(usuarioId))
            .ReturnsAsync(conversacionesEsperadas);

        var controller = CrearController(usuarioId);

        //Act
        var resultado = await controller.ObtenerConversaciones(CancellationToken.None);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var conversaciones = Assert.IsAssignableFrom<IEnumerable<ConversacionResumenDto>>(ok.Value);
        Assert.Single(conversaciones);
        _chatServicioMock.Verify(s => s.ObtenerConversacionesActivasAsync(usuarioId), Times.Once);
    }

    [Fact]
    public async Task QueObtenerConversacionesSinAutenticarLanceUnauthorizedAccessException()
    {
        //Arrange
        var controller = CrearController(usuarioId: null);

        //Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.ObtenerConversaciones(CancellationToken.None));

        _chatServicioMock.Verify(s => s.ObtenerConversacionesActivasAsync(It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region Eliminar Conversacion

    [Fact]
    public async Task QueEliminarConversacionAutenticadoInvoqueServicioYDevuelvaNoContent()
    {
        //Arrange
        const int usuarioId = 5;
        const int otroUsuarioId = 12;

        _chatServicioMock
            .Setup(s => s.EliminarConversacionAsync(usuarioId, otroUsuarioId))
            .Returns(Task.CompletedTask);

        var controller = CrearController(usuarioId);

        //Act
        var resultado = await controller.EliminarConversacion(otroUsuarioId, CancellationToken.None);

        //Assert
        Assert.IsType<NoContentResult>(resultado);
        _chatServicioMock.Verify(s => s.EliminarConversacionAsync(usuarioId, otroUsuarioId), Times.Once);
    }

    [Fact]
    public async Task QueEliminarConversacionSinAutenticarLanceUnauthorizedAccessException()
    {
        //Arrange
        var controller = CrearController(usuarioId: null);

        //Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.EliminarConversacion(12, CancellationToken.None));

        _chatServicioMock.Verify(s => s.EliminarConversacionAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private ChatController CrearController(int? usuarioId)
    {
        var controller = new ChatController(_chatServicioMock.Object);
        var claims = new List<Claim>();

        if (usuarioId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, usuarioId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, usuarioId.HasValue ? "TestAuth" : null);
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    #endregion
}

