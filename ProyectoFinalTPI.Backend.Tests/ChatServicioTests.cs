using Moq;
using ProyectoFinalTPI.Backend.Dtos.Chat;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ChatServicioTests
{
    private readonly Mock<IMensajeChatRepositorio> _mensajeChatRepositorioMock = new();
    private readonly Mock<INotificacionServicio> _notificacionServicioMock = new();
    private readonly ChatServicio _servicio;

    public ChatServicioTests()
    {
        _servicio = new ChatServicio(_mensajeChatRepositorioMock.Object, _notificacionServicioMock.Object);
    }

    #region Enviar Mensaje

    [Fact]
    public async Task QueEnviarMensajeLlameARepositorioConDatosCorrectos()
    {
        //Arrange
        var emisorId = 1;
        var emisorUsername = "usuarioA";
        var destinatarioId = 2;
        var contenido = "Hola!";

        //Act
        await _servicio.EnviarMensajeAsync(
            emisorId: emisorId,
            emisorUsername: emisorUsername,
            destinatarioId: destinatarioId,
            contenido: contenido);

        //Assert
        _mensajeChatRepositorioMock.Verify(r => r.GuardarAsync(
            It.Is<MensajeChat>(m =>
                m.EmisorId == 1 &&
                m.DestinatarioId == 2 &&
                m.Contenido == "Hola!"
            )), Times.Once);
    }

    [Fact]
    public async Task QueEnviarMensajeDevuelvaDtoConDatosDelMensaje()
    {
        //Arrange
        var emisorId = 1;
        var emisorUsername = "usuarioA";
        var destinatarioId = 2;
        var contenido = "Hola!";

        //Act
        var resultado = await _servicio.EnviarMensajeAsync(
            emisorId: emisorId,
            emisorUsername: emisorUsername,
            destinatarioId: destinatarioId,
            contenido: contenido);

        //Assert
        Assert.Equal(1, resultado.EmisorId);
        Assert.Equal("usuarioA", resultado.EmisorUsername);
        Assert.Equal(2, resultado.DestinatarioId);
        Assert.Equal("Hola!", resultado.Contenido);
    }

    [Fact]
    public async Task QueEnviarMensajeFijeFechaExpiracionEnSieteDias()
    {
        //Arrange
        var antes = DateTime.UtcNow;

        //Act
        await _servicio.EnviarMensajeAsync(1, "usuarioA", 2, "Hola!");
        var despues = DateTime.UtcNow;

        //Assert
        _mensajeChatRepositorioMock.Verify(r => r.GuardarAsync(
            It.Is<MensajeChat>(m =>
                m.FechaExpiracion >= antes.AddDays(7) &&
                m.FechaExpiracion <= despues.AddDays(7).AddSeconds(1)
            )), Times.Once);
    }

    [Fact]
    public async Task QueEnviarMensajeFechaEnvioSeaUtcReciente()
    {
        //Arrange
        var antes = DateTime.UtcNow;

        //Act
        var resultado = await _servicio.EnviarMensajeAsync(1, "usuarioA", 2, "Hola!");
        var despues = DateTime.UtcNow;

        //Assert
        Assert.InRange(resultado.FechaEnvio, antes, despues);
    }

    #endregion

    #region Obtener Historial

    [Fact]
    public async Task QueObtenerHistorialDevuelvaMensajesMapeadosADto()
    {
        //Arrange
        var emisorFalso = new Usuario { Id = 1, Username = "usuarioA" };
        var mensajesFalsos = new List<MensajeChat>
        {
            new() {
                Id = 10,
                EmisorId = 1,
                Emisor = emisorFalso,
                DestinatarioId = 2,
                Contenido = "Primer mensaje",
                FechaEnvio = DateTime.UtcNow.AddMinutes(-10)
            },
            new() {
                Id = 11,
                EmisorId = 1,
                Emisor = emisorFalso,
                DestinatarioId = 2,
                Contenido = "Segundo mensaje",
                FechaEnvio = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        _mensajeChatRepositorioMock
            .Setup(r => r.ObtenerHistorialAsync(1, 2))
            .ReturnsAsync(mensajesFalsos);

        //Act
        var resultado = (await _servicio.ObtenerHistorialAsync(1, 2)).ToList();

        //Assert
        Assert.Equal(2, resultado.Count);
        Assert.Equal("Primer mensaje", resultado[0].Contenido);
        Assert.Equal("usuarioA", resultado[0].EmisorUsername);
        Assert.Equal("Segundo mensaje", resultado[1].Contenido);
        _mensajeChatRepositorioMock.Verify(r => r.ObtenerHistorialAsync(1, 2), Times.Once);
    }

    [Fact]
    public async Task QueObtenerHistorialSinMensajesDevuelvaListaVacia()
    {
        //Arrange
        _mensajeChatRepositorioMock
            .Setup(r => r.ObtenerHistorialAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<MensajeChat>());

        //Act
        var resultado = await _servicio.ObtenerHistorialAsync(1, 2);

        //Assert
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task QueObtenerHistorialLlameAlRepositorioConLosIdsCorrectos()
    {
        //Arrange
        _mensajeChatRepositorioMock
            .Setup(r => r.ObtenerHistorialAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<MensajeChat>());

        //Act
        await _servicio.ObtenerHistorialAsync(usuarioAId: 5, usuarioBId: 7);

        //Assert
        _mensajeChatRepositorioMock.Verify(r => r.ObtenerHistorialAsync(5, 7), Times.Once);
    }

    [Fact]
    public async Task QueObtenerHistorialConEmisorNullUseStringVacioParaUsername()
    {
        //Arrange
        var mensajesSinNavigation = new List<MensajeChat>
        {
            new() {
                Id = 1,
                EmisorId = 1,
                Emisor = null!,
                DestinatarioId = 2,
                Contenido = "Mensaje",
                FechaEnvio = DateTime.UtcNow
            }
        };

        _mensajeChatRepositorioMock
            .Setup(r => r.ObtenerHistorialAsync(1, 2))
            .ReturnsAsync(mensajesSinNavigation);

        //Act
        var resultado = (await _servicio.ObtenerHistorialAsync(1, 2)).ToList();

        //Assert
        Assert.Equal(string.Empty, resultado[0].EmisorUsername);
    }

    #endregion
}
