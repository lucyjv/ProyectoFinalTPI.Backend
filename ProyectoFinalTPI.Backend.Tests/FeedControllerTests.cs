using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class FeedControllerTests
{
    private readonly Mock<IPublicacionServicio> _servicioMock = new();
    private readonly FeedController _controller;

    public FeedControllerTests()
    {
        _controller = new FeedController(_servicioMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    #region Registro de Impresiones

    [Fact]
    public async Task QueRegistrarImpresionesUseElUsuarioDelTokenYDevuelveNoContent()
    {
        //Arrange
        Autenticar(10);
        int? usuarioRecibido = null;
        int[]? idsRecibidos = null;
        _servicioMock.Setup(servicio => servicio.RegistrarImpresionesFeed(
                It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyCollection<int>, CancellationToken>((usuarioId, ids, _) =>
            {
                usuarioRecibido = usuarioId;
                idsRecibidos = ids.ToArray();
            })
            .Returns(Task.CompletedTask);

        var request = new RegistrarImpresionesFeedRequest { PublicacionesIds = new[] { 2, 3 } };

        //Act
        var resultado = await _controller.RegistrarImpresiones(request);

        //Assert
        Assert.IsType<NoContentResult>(resultado);
        Assert.Equal(10, usuarioRecibido);
        Assert.Equal(new[] { 2, 3 }, idsRecibidos);
    }

    [Fact]
    public async Task QueRegistrarImpresionesSinTokenDevuelvaUnauthorized()
    {
        //Arrange
        var request = new RegistrarImpresionesFeedRequest { PublicacionesIds = new[] { 2 } };

        //Act
        var resultado = await _controller.RegistrarImpresiones(request);

        //Assert
        Assert.IsType<UnauthorizedObjectResult>(resultado);
        _servicioMock.Verify(servicio => servicio.RegistrarImpresionesFeed(
            It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueRegistrarImpresionesSinBodyDevuelvaBadRequest()
    {
        //Arrange
        Autenticar(10);

        //Act
        var resultado = await _controller.RegistrarImpresiones(null!);

        //Assert
        Assert.IsType<BadRequestObjectResult>(resultado);
        _servicioMock.Verify(servicio => servicio.RegistrarImpresionesFeed(
            It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueRegistrarImpresionesConListaInvalidaDevuelvaBadRequest()
    {
        //Arrange
        Autenticar(10);
        _servicioMock.Setup(servicio => servicio.RegistrarImpresionesFeed(
                10, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Lista inválida"));

        var request = new RegistrarImpresionesFeedRequest();

        //Act
        var resultado = await _controller.RegistrarImpresiones(request);

        //Assert
        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task QueRegistrarImpresionesConUsuarioInexistenteDevuelvaNotFound()
    {
        //Arrange
        Autenticar(10);
        _servicioMock.Setup(servicio => servicio.RegistrarImpresionesFeed(
                10, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Usuario inexistente"));

        var request = new RegistrarImpresionesFeedRequest { PublicacionesIds = new[] { 2 } };

        //Act
        var resultado = await _controller.RegistrarImpresiones(request);

        //Assert
        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private void Autenticar(int usuarioId)
    {
        _controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString())
        }, "Test"));
    }

    #endregion
}
