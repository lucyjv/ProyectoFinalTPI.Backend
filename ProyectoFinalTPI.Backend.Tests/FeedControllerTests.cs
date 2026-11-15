using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class FeedControllerTests
{
    private readonly Mock<IPublicacionServicio> _servicioMock = new();
    private readonly Mock<IEtiquetadoPublicacionServicio> _etiquetadoMock = new();
    private readonly FeedController _controller;

    public FeedControllerTests()
    {
        _controller = new FeedController(_servicioMock.Object, _etiquetadoMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    #region Feed Para Vos

    [Fact]
    public async Task QueObtenerParaVosSinUsuarioDevuelvaUnauthorized()
    {
        //Arrange (sin token y sin query param)

        //Act
        var resultado = await _controller.ObtenerParaVos(usuarioId: null);

        //Assert
        Assert.IsType<UnauthorizedObjectResult>(resultado.Result);
        _servicioMock.Verify(s => s.ObtenerPublicacionesParaVos(
            It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueObtenerParaVosConUsuarioIdEnQueryDevuelvaOk()
    {
        //Arrange
        const int usuarioId = 15;
        var paginado = new ResultadoPaginadoDto<PublicacionDetalleDto>
        {
            Items = new List<PublicacionDetalleDto> { new() { Id = 1, Titulo = "Recuerdo" } }
        };

        _servicioMock
            .Setup(s => s.ObtenerPublicacionesParaVos(usuarioId, null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginado);

        //Act
        var resultado = await _controller.ObtenerParaVos(usuarioId: usuarioId);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var obtenido = Assert.IsType<ResultadoPaginadoDto<PublicacionDetalleDto>>(ok.Value);
        Assert.Single(obtenido.Items);
    }

    [Fact]
    public async Task QueObtenerParaVosAutenticadoDevuelvaOkYCompleteEtiquetas()
    {
        //Arrange
        const int usuarioId = 20;
        Autenticar(usuarioId);

        var paginado = new ResultadoPaginadoDto<PublicacionDetalleDto>
        {
            Items = new List<PublicacionDetalleDto> { new() { Id = 10, Titulo = "Viaje" } }
        };

        _servicioMock
            .Setup(s => s.ObtenerPublicacionesParaVos(usuarioId, "cursor1", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginado);

        //Act
        var resultado = await _controller.ObtenerParaVos(cursor: "cursor1", limite: 10);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        Assert.Same(paginado, ok.Value);
        _etiquetadoMock.Verify(e => e.CompletarEtiquetas(paginado.Items, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueObtenerParaVosConSesionExpiradaDevuelvaStatus410Gone()
    {
        //Arrange
        Autenticar(10);
        _servicioMock
            .Setup(s => s.ObtenerPublicacionesParaVos(10, It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SesionFypNoDisponibleException());

        //Act
        var resultado = await _controller.ObtenerParaVos();

        //Assert
        var objectResult = Assert.IsType<ObjectResult>(resultado.Result);
        Assert.Equal(StatusCodes.Status410Gone, objectResult.StatusCode);
    }

    [Fact]
    public async Task QueObtenerParaVosConArgumentExceptionDevuelvaBadRequest()
    {
        //Arrange
        Autenticar(10);
        _servicioMock
            .Setup(s => s.ObtenerPublicacionesParaVos(10, It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Parámetros inválidos"));

        //Act
        var resultado = await _controller.ObtenerParaVos();

        //Assert
        Assert.IsType<BadRequestObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task QueObtenerParaVosConKeyNotFoundExceptionDevuelvaNotFound()
    {
        //Arrange
        Autenticar(10);
        _servicioMock
            .Setup(s => s.ObtenerPublicacionesParaVos(10, It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Usuario no encontrado"));

        //Act
        var resultado = await _controller.ObtenerParaVos();

        //Assert
        Assert.IsType<NotFoundObjectResult>(resultado.Result);
    }

    #endregion

    #region Feed Seguidos

    [Fact]
    public async Task QueObtenerSeguidosSinUsuarioDevuelvaUnauthorized()
    {
        //Arrange

        //Act
        var resultado = await _controller.ObtenerSeguidos(usuarioId: null);

        //Assert
        Assert.IsType<UnauthorizedObjectResult>(resultado.Result);
        _servicioMock.Verify(s => s.ObtenerFeedSeguidos(
            It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueObtenerSeguidosAutenticadoDevuelvaOkConPublicaciones()
    {
        //Arrange
        const int usuarioId = 25;
        Autenticar(usuarioId);

        var paginado = new ResultadoPaginadoDto<PublicacionDetalleDto>
        {
            Items = new List<PublicacionDetalleDto> { new() { Id = 5, Titulo = "Seguido" } }
        };

        _servicioMock
            .Setup(s => s.ObtenerFeedSeguidos(usuarioId, null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginado);

        //Act
        var resultado = await _controller.ObtenerSeguidos();

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        Assert.Same(paginado, ok.Value);
        _servicioMock.Verify(s => s.ObtenerFeedSeguidos(usuarioId, null, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueObtenerSeguidosConArgumentExceptionDevuelvaBadRequest()
    {
        //Arrange
        Autenticar(25);
        _servicioMock
            .Setup(s => s.ObtenerFeedSeguidos(25, It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Límite fuera de rango"));

        //Act
        var resultado = await _controller.ObtenerSeguidos();

        //Assert
        Assert.IsType<BadRequestObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task QueObtenerSeguidosConKeyNotFoundExceptionDevuelvaNotFound()
    {
        //Arrange
        Autenticar(25);
        _servicioMock
            .Setup(s => s.ObtenerFeedSeguidos(25, It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Usuario no existe"));

        //Act
        var resultado = await _controller.ObtenerSeguidos();

        //Assert
        Assert.IsType<NotFoundObjectResult>(resultado.Result);
    }

    #endregion

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
