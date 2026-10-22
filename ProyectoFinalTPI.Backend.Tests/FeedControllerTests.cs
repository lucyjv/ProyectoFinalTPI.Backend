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
    private readonly Mock<IPublicacionServicio> _servicio = new();
    private readonly FeedController _controller;

    public FeedControllerTests()
    {
        _controller = new FeedController(_servicio.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task RegistrarImpresionesUsaElUsuarioDelTokenYDevuelveNoContent()
    {
        Autenticar(10);
        int? usuarioRecibido = null;
        int[]? idsRecibidos = null;
        _servicio.Setup(servicio => servicio.RegistrarImpresionesFeed(
                It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyCollection<int>, CancellationToken>((usuarioId, ids, _) =>
            {
                usuarioRecibido = usuarioId;
                idsRecibidos = ids.ToArray();
            })
            .Returns(Task.CompletedTask);

        var resultado = await _controller.RegistrarImpresiones(
            new RegistrarImpresionesFeedRequest { PublicacionesIds = new[] { 2, 3 } });

        Assert.IsType<NoContentResult>(resultado);
        Assert.Equal(10, usuarioRecibido);
        Assert.Equal(new[] { 2, 3 }, idsRecibidos);
    }

    [Fact]
    public async Task RegistrarImpresionesSinTokenDevuelveUnauthorized()
    {
        var resultado = await _controller.RegistrarImpresiones(
            new RegistrarImpresionesFeedRequest { PublicacionesIds = new[] { 2 } });

        Assert.IsType<UnauthorizedObjectResult>(resultado);
        _servicio.Verify(servicio => servicio.RegistrarImpresionesFeed(
            It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarImpresionesSinBodyDevuelveBadRequest()
    {
        Autenticar(10);

        var resultado = await _controller.RegistrarImpresiones(null!);

        Assert.IsType<BadRequestObjectResult>(resultado);
        _servicio.Verify(servicio => servicio.RegistrarImpresionesFeed(
            It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarImpresionesConListaInvalidaDevuelveBadRequest()
    {
        Autenticar(10);
        _servicio.Setup(servicio => servicio.RegistrarImpresionesFeed(
                10, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Lista inválida"));

        var resultado = await _controller.RegistrarImpresiones(new RegistrarImpresionesFeedRequest());

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task RegistrarImpresionesConUsuarioInexistenteDevuelveNotFound()
    {
        Autenticar(10);
        _servicio.Setup(servicio => servicio.RegistrarImpresionesFeed(
                10, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Usuario inexistente"));

        var resultado = await _controller.RegistrarImpresiones(
            new RegistrarImpresionesFeedRequest { PublicacionesIds = new[] { 2 } });

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    private void Autenticar(int usuarioId)
    {
        _controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString())
        }, "Test"));
    }
}
