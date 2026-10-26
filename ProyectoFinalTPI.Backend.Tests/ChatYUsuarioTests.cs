using Moq;
using Xunit;
using ProyectoFinalTPI.Backend.Dtos.Chat;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;

namespace ProyectoFinalTPI.Backend.Tests;

// ═══════════════════════════════════════════════════════════════════════════
//  ChatServicio — ObtenerConversacionesActivasAsync
// ═══════════════════════════════════════════════════════════════════════════

public class ChatServicioObtenerConversacionesTests
{
    private readonly Mock<IMensajeChatRepositorio> _repositorioMock;
    private readonly IChatServicio _servicio;

    public ChatServicioObtenerConversacionesTests()
    {
        _repositorioMock = new Mock<IMensajeChatRepositorio>();
        _servicio = new ChatServicio(_repositorioMock.Object, Mock.Of<INotificacionServicio>());
    }

    [Fact]
    public async Task ObtenerConversacionesActivasAsync_SinMensajes_DevuelveListaVacia()
    {
        // Arrange
        _repositorioMock
            .Setup(r => r.ObtenerConversacionesActivasAsync(1))
            .ReturnsAsync(Enumerable.Empty<MensajeChat>());

        // Act
        var resultado = await _servicio.ObtenerConversacionesActivasAsync(1);

        // Assert
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ObtenerConversacionesActivasAsync_ConMensajes_DevuelveConversacionesOrdenadas()
    {
        // Arrange: usuario 1 tiene conversaciones con usuario 2 y usuario 3.
        var usuario1 = new Usuario { Id = 1, Username = "ana" };
        var usuario2 = new Usuario { Id = 2, Username = "lucia" };
        var usuario3 = new Usuario { Id = 3, Username = "pedro" };

        var ahora = DateTime.UtcNow;
        var mensajes = new List<MensajeChat>
        {
            // Último mensaje con lucia (usuario 1 envió)
            new()
            {
                Id = 10, EmisorId = 1, Emisor = usuario1,
                DestinatarioId = 2, Destinatario = usuario2,
                Contenido = "Hola lucia", FechaEnvio = ahora.AddHours(-1),
                FechaExpiracion = ahora.AddDays(6)
            },
            // Último mensaje con pedro (pedro envió)
            new()
            {
                Id = 20, EmisorId = 3, Emisor = usuario3,
                DestinatarioId = 1, Destinatario = usuario1,
                Contenido = "¿Qué tal?", FechaEnvio = ahora.AddHours(-2),
                FechaExpiracion = ahora.AddDays(5)
            }
        };

        _repositorioMock
            .Setup(r => r.ObtenerConversacionesActivasAsync(1))
            .ReturnsAsync(mensajes);

        // Act
        var resultado = (await _servicio.ObtenerConversacionesActivasAsync(1)).ToList();

        // Assert
        Assert.Equal(2, resultado.Count);

        var conLucia = resultado.Single(c => c.OtroUsuarioId == 2);
        Assert.Equal("lucia", conLucia.OtroUsuarioUsername);
        Assert.Equal("Hola lucia", conLucia.UltimoMensaje);

        var conPedro = resultado.Single(c => c.OtroUsuarioId == 3);
        Assert.Equal("pedro", conPedro.OtroUsuarioUsername);
        Assert.Equal("¿Qué tal?", conPedro.UltimoMensaje);
    }

    [Fact]
    public async Task ObtenerConversacionesActivasAsync_UsuarioEsDestinatario_ReconoceOtroUsuarioCorrectamente()
    {
        // Arrange: usuario 5 recibe un mensaje de usuario 7.
        var usuario5 = new Usuario { Id = 5, Username = "mario" };
        var usuario7 = new Usuario { Id = 7, Username = "jose" };

        var ahora = DateTime.UtcNow;
        var mensajes = new List<MensajeChat>
        {
            new()
            {
                Id = 30, EmisorId = 7, Emisor = usuario7,
                DestinatarioId = 5, Destinatario = usuario5,
                Contenido = "Mensaje de jose", FechaEnvio = ahora.AddMinutes(-10),
                FechaExpiracion = ahora.AddDays(7)
            }
        };

        _repositorioMock
            .Setup(r => r.ObtenerConversacionesActivasAsync(5))
            .ReturnsAsync(mensajes);

        // Act
        var resultado = (await _servicio.ObtenerConversacionesActivasAsync(5)).ToList();

        // Assert
        Assert.Single(resultado);
        Assert.Equal(7, resultado[0].OtroUsuarioId);
        Assert.Equal("jose", resultado[0].OtroUsuarioUsername);
        Assert.Equal("Mensaje de jose", resultado[0].UltimoMensaje);
    }

    [Fact]
    public async Task ObtenerConversacionesActivasAsync_LlamaAlRepositorioUnaVez()
    {
        // Arrange
        _repositorioMock
            .Setup(r => r.ObtenerConversacionesActivasAsync(It.IsAny<int>()))
            .ReturnsAsync(Enumerable.Empty<MensajeChat>());

        // Act
        await _servicio.ObtenerConversacionesActivasAsync(99);

        // Assert
        _repositorioMock.Verify(
            r => r.ObtenerConversacionesActivasAsync(99),
            Times.Once);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  ChatServicio — EliminarConversacionAsync
// ═══════════════════════════════════════════════════════════════════════════

public class ChatServicioEliminarConversacionTests
{
    private readonly Mock<IMensajeChatRepositorio> _repositorioMock;
    private readonly IChatServicio _servicio;

    public ChatServicioEliminarConversacionTests()
    {
        _repositorioMock = new Mock<IMensajeChatRepositorio>();
        _servicio = new ChatServicio(_repositorioMock.Object, Mock.Of<INotificacionServicio>());
    }

    [Fact]
    public async Task EliminarConversacionAsync_MismoUsuario_LanzaArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _servicio.EliminarConversacionAsync(5, 5));
    }

    [Fact]
    public async Task EliminarConversacionAsync_MismoUsuario_NoConsultaElRepositorio()
    {
        // Act
        await Record.ExceptionAsync(() => _servicio.EliminarConversacionAsync(5, 5));

        // Assert
        _repositorioMock.Verify(
            r => r.EliminarConversacionAsync(It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task EliminarConversacionAsync_SinMensajes_LanzaKeyNotFoundException()
    {
        // Arrange: el repositorio informa que no había nada que borrar.
        _repositorioMock
            .Setup(r => r.EliminarConversacionAsync(1, 2))
            .ReturnsAsync(0);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _servicio.EliminarConversacionAsync(1, 2));
    }

    [Fact]
    public async Task EliminarConversacionAsync_ConMensajes_CompletaSinExcepcion()
    {
        // Arrange
        _repositorioMock
            .Setup(r => r.EliminarConversacionAsync(1, 2))
            .ReturnsAsync(3);

        // Act & Assert: no debe lanzar
        var ex = await Record.ExceptionAsync(
            () => _servicio.EliminarConversacionAsync(1, 2));

        Assert.Null(ex);
    }

    [Fact]
    public async Task EliminarConversacionAsync_LlamaAlRepositorioUnaVez()
    {
        // Arrange
        _repositorioMock
            .Setup(r => r.EliminarConversacionAsync(1, 2))
            .ReturnsAsync(1);

        // Act
        await _servicio.EliminarConversacionAsync(1, 2);

        // Assert
        _repositorioMock.Verify(
            r => r.EliminarConversacionAsync(1, 2),
            Times.Once);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  UsuarioServicio — ObtenerPorUsernameAsync
// ═══════════════════════════════════════════════════════════════════════════

public class UsuarioServicioTests
{
    private readonly Mock<IUsuarioRepositorio> _repositorioMock;
    private readonly IUsuarioServicio _servicio;

    public UsuarioServicioTests()
    {
        _repositorioMock = new Mock<IUsuarioRepositorio>();
        _servicio = new UsuarioServicio(_repositorioMock.Object, Mock.Of<ILogroServicio>());
    }

    [Fact]
    public async Task ObtenerPorUsernameAsync_UsuarioExiste_DevuelveDto()
    {
        // Arrange
        var usuario = new Usuario { Id = 2, Username = "lucia" };
        _repositorioMock
            .Setup(r => r.ObtenerPorUsernameAsync("lucia", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        // Act
        var resultado = await _servicio.ObtenerPorUsernameAsync("lucia");

        // Assert
        Assert.Equal(2, resultado.UsuarioId);
        Assert.Equal("lucia", resultado.Username);
    }

    [Fact]
    public async Task ObtenerPorUsernameAsync_UsuarioNoExiste_LanzaKeyNotFoundException()
    {
        // Arrange
        _repositorioMock
            .Setup(r => r.ObtenerPorUsernameAsync("noexiste", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _servicio.ObtenerPorUsernameAsync("noexiste"));
    }

    [Fact]
    public async Task ObtenerPorUsernameAsync_LanzaKeyNotFoundException_MensajeContieneUsername()
    {
        // Arrange
        _repositorioMock
            .Setup(r => r.ObtenerPorUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        // Act
        var ex = await Record.ExceptionAsync(
            () => _servicio.ObtenerPorUsernameAsync("fantasma"));

        // Assert
        Assert.IsType<KeyNotFoundException>(ex);
        Assert.Contains("fantasma", ex.Message);
    }

    [Fact]
    public async Task ObtenerPorUsernameAsync_LlamaAlRepositorioUnaVez()
    {
        // Arrange
        _repositorioMock
            .Setup(r => r.ObtenerPorUsernameAsync("lucia", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuario { Id = 2, Username = "lucia" });

        // Act
        await _servicio.ObtenerPorUsernameAsync("lucia");

        // Assert
        _repositorioMock.Verify(
            r => r.ObtenerPorUsernameAsync("lucia", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuscarAsync_NormalizaYDelegaLaBusquedaParcial()
    {
        var usuarios = new List<AutorResumenDto>
        {
            new() { Id = 2, Username = "lucia" }
        };
        _repositorioMock
            .Setup(r => r.BuscarPorUsernameAsync("luci", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuarios);

        var resultado = await _servicio.BuscarAsync("  luci  ", 5);

        Assert.Same(usuarios, resultado);
        _repositorioMock.Verify(
            r => r.BuscarPorUsernameAsync("luci", 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("a", 10)]
    [InlineData("usuario", 0)]
    [InlineData("usuario", 21)]
    public async Task BuscarAsync_RechazaParametrosInvalidos(string texto, int limite)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _servicio.BuscarAsync(texto, limite));
        _repositorioMock.Verify(
            r => r.BuscarPorUsernameAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ObtenerPerfilPorUsernameAsync_UsuarioPersonalExiste_DevuelvePerfilCompleto()
    {
        // Arrange
        var usuario = new Usuario_Personal
        {
            Id = 17,
            Username = "nico",
            Nombre = "Nico",
            Apellido = "Pérez",
            Reputacion = ReputacionEnum.Nostalgico,
            PuntosNostalgia = 10
        };
        _repositorioMock
            .Setup(r => r.ObtenerPorUsernameAsync("nico", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _repositorioMock
            .Setup(r => r.ContarPublicacionesVisiblesAsync(17, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var resultado = await _servicio.ObtenerPerfilPorUsernameAsync("nico", null);

        // Assert
        Assert.Equal(17, resultado.Id);
        Assert.Equal("nico", resultado.Username);
        Assert.Equal("Nico", resultado.Nombre);
        Assert.Equal("Nostálgico", resultado.Reputacion);
        Assert.Equal(10, resultado.PuntosNostalgia);
        Assert.Equal(1, resultado.CantidadPublicaciones);
    }
}
