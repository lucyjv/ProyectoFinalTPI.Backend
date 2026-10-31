using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class UsuarioControllerTests
{
    private readonly Mock<IUsuarioServicio> _usuarioServicioMock = new();
    private readonly UsuarioController _controller;

    public UsuarioControllerTests()
    {
        _controller = new UsuarioController(_usuarioServicioMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    #region Perfil Propio (GET /api/usuario/me/perfil)

    [Fact]
    public async Task QueObtenerMiPerfilDevuelvaUnauthorizedSiNoEstaAutenticado()
    {
        //Act
        var resultado = await _controller.ObtenerMiPerfil(CancellationToken.None);

        //Assert
        Assert.IsType<UnauthorizedResult>(resultado);
        _usuarioServicioMock.Verify(s => s.ObtenerPerfilPorIdAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueObtenerMiPerfilDevuelvaOkConDtoSiEstaAutenticado()
    {
        //Arrange
        AutenticarComo(10);
        var perfilMock = CrearPerfilDto(10, "usuario10");
        _usuarioServicioMock
            .Setup(s => s.ObtenerPerfilPorIdAsync(10, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(perfilMock);

        //Act
        var resultado = await _controller.ObtenerMiPerfil(CancellationToken.None);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var dto = Assert.IsType<UsuarioPerfilDto>(ok.Value);
        Assert.Equal(10, dto.Id);
        Assert.Equal("usuario10", dto.Username);
        _usuarioServicioMock.Verify(s => s.ObtenerPerfilPorIdAsync(10, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Cambiar Username (PATCH /api/usuario/me/username)

    [Fact]
    public async Task QueCambiarMiUsernameDevuelvaUnauthorizedSiNoEstaAutenticado()
    {
        //Arrange
        var dto = new CambiarUsernameDto { Username = "nuevoNombre" };

        //Act
        var resultado = await _controller.CambiarMiUsername(dto, CancellationToken.None);

        //Assert
        Assert.IsType<UnauthorizedResult>(resultado);
        _usuarioServicioMock.Verify(s => s.CambiarUsernamePropioAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueCambiarMiUsernameDevuelvaOkConDtoSiEstaAutenticado()
    {
        //Arrange
        AutenticarComo(10);
        var dto = new CambiarUsernameDto { Username = "nuevoNombre" };
        var perfilActualizado = CrearPerfilDto(10, "nuevoNombre");

        _usuarioServicioMock
            .Setup(s => s.CambiarUsernamePropioAsync(10, "nuevoNombre", It.IsAny<CancellationToken>()))
            .ReturnsAsync(perfilActualizado);

        //Act
        var resultado = await _controller.CambiarMiUsername(dto, CancellationToken.None);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var perfil = Assert.IsType<UsuarioPerfilDto>(ok.Value);
        Assert.Equal("nuevoNombre", perfil.Username);
        _usuarioServicioMock.Verify(s => s.CambiarUsernamePropioAsync(10, "nuevoNombre", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Cambiar Foto de Perfil (PUT /api/usuario/me/foto)

    [Fact]
    public async Task QueCambiarMiFotoDevuelvaUnauthorizedSiNoEstaAutenticado()
    {
        //Arrange
        var archivo = CrearFormFile(1024);

        //Act
        var resultado = await _controller.CambiarMiFoto(archivo, CancellationToken.None);

        //Assert
        Assert.IsType<UnauthorizedResult>(resultado);
    }

    [Fact]
    public async Task QueCambiarMiFotoDevuelvaBadRequestSiArchivoEsNulo()
    {
        //Arrange
        AutenticarComo(10);

        //Act
        var resultado = await _controller.CambiarMiFoto(null!, CancellationToken.None);

        //Assert
        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task QueCambiarMiFotoDevuelvaBadRequestSiArchivoEstaVacio()
    {
        //Arrange
        AutenticarComo(10);
        var archivoVacio = CrearFormFile(0);

        //Act
        var resultado = await _controller.CambiarMiFoto(archivoVacio, CancellationToken.None);

        //Assert
        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task QueCambiarMiFotoDevuelvaBadRequestSiArchivoSupera2MiB()
    {
        //Arrange
        AutenticarComo(10);
        var archivoGrande = CrearFormFile(3 * 1024 * 1024);

        //Act
        var resultado = await _controller.CambiarMiFoto(archivoGrande, CancellationToken.None);

        //Assert
        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task QueCambiarMiFotoDevuelvaOkSiArchivoEsValido()
    {
        //Arrange
        AutenticarComo(10);
        var archivoValido = CrearFormFile(1024, "image/jpeg");
        var perfilActualizado = CrearPerfilDto(10, "usuario10");

        _usuarioServicioMock
            .Setup(s => s.CambiarFotoPerfilPropiaAsync(10, It.IsAny<Stream>(), "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(perfilActualizado);

        //Act
        var resultado = await _controller.CambiarMiFoto(archivoValido, CancellationToken.None);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var perfil = Assert.IsType<UsuarioPerfilDto>(ok.Value);
        Assert.Equal(10, perfil.Id);
        _usuarioServicioMock.Verify(s => s.CambiarFotoPerfilPropiaAsync(10, It.IsAny<Stream>(), "image/jpeg", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Consultas y Búsquedas

    [Fact]
    public async Task QueObtenerPorUsernameDevuelvaOkConDto()
    {
        //Arrange
        var resumen = new UsuarioResumenDto { UsuarioId = 10, Username = "juan" };
        _usuarioServicioMock
            .Setup(s => s.ObtenerPorUsernameAsync("juan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resumen);

        //Act
        var resultado = await _controller.ObtenerPorUsername("juan", CancellationToken.None);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var dto = Assert.IsType<UsuarioResumenDto>(ok.Value);
        Assert.Equal(10, dto.UsuarioId);
        Assert.Equal("juan", dto.Username);
        _usuarioServicioMock.Verify(s => s.ObtenerPorUsernameAsync("juan", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueBuscarDevuelvaOkConListaDeAutores()
    {
        //Arrange
        var autores = new List<AutorResumenDto>
        {
            new() { Id = 1, Username = "juan1" },
            new() { Id = 2, Username = "juan2" }
        };
        _usuarioServicioMock
            .Setup(s => s.BuscarAsync("juan", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(autores);

        //Act
        var resultado = await _controller.Buscar("juan", 10, CancellationToken.None);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var lista = Assert.IsType<List<AutorResumenDto>>(ok.Value);
        Assert.Equal(2, lista.Count);
        _usuarioServicioMock.Verify(s => s.BuscarAsync("juan", 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueObtenerPerfilPorUsernameDevuelvaOkConDto()
    {
        //Arrange
        AutenticarComo(5);
        var perfil = CrearPerfilDto(10, "pedro");
        _usuarioServicioMock
            .Setup(s => s.ObtenerPerfilPorUsernameAsync("pedro", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(perfil);

        //Act
        var resultado = await _controller.ObtenerPerfilPorUsername("pedro", CancellationToken.None);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var dto = Assert.IsType<UsuarioPerfilDto>(ok.Value);
        Assert.Equal(10, dto.Id);
        Assert.Equal("pedro", dto.Username);
        _usuarioServicioMock.Verify(s => s.ObtenerPerfilPorUsernameAsync("pedro", 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueObtenerPerfilPorIdDevuelvaOkConDto()
    {
        //Arrange
        AutenticarComo(5);
        var perfil = CrearPerfilDto(10, "pedro");
        _usuarioServicioMock
            .Setup(s => s.ObtenerPerfilPorIdAsync(10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(perfil);

        //Act
        var resultado = await _controller.ObtenerPerfilPorId(10, CancellationToken.None);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var dto = Assert.IsType<UsuarioPerfilDto>(ok.Value);
        Assert.Equal(10, dto.Id);
        Assert.Equal("pedro", dto.Username);
        _usuarioServicioMock.Verify(s => s.ObtenerPerfilPorIdAsync(10, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helpers Privados

    private void AutenticarComo(int usuarioId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    private static UsuarioPerfilDto CrearPerfilDto(int id, string username) => new()
    {
        Id = id,
        Username = username
    };

    private static IFormFile CrearFormFile(long length, string contentType = "image/png")
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.Length).Returns(length);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length]));
        return mock.Object;
    }

    #endregion
}

