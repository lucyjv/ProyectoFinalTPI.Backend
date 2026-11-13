using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class RecuperacionContrasenaControllerTests
{
    private readonly Mock<IAuthServicio> _auth = new();

    private AuthController CrearController(string entorno = "Testing")
    {
        var ambiente = new Mock<IWebHostEnvironment>();
        ambiente.SetupGet(e => e.EnvironmentName).Returns(entorno);
        return new(_auth.Object, ambiente.Object, Mock.Of<IUsuarioRepositorio>())
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task QueLaSolicitudDevuelvaElMismoMensajeSinRevelarSiLaCuentaExiste()
    {
        //Arrange
        _auth.Setup(a => a.SolicitarRecuperacionAsync(It.IsAny<RecuperarContrasenaDto>(), default))
            .Returns(Task.CompletedTask);
        var controller = CrearController();

        //Act
        var existente = Assert.IsType<AcceptedResult>(await controller.RecuperarContrasena(
            new() { Email = "existente@example.com" }, default));
        var inexistente = Assert.IsType<AcceptedResult>(await controller.RecuperarContrasena(
            new() { Email = "inexistente@example.com" }, default));

        //Assert
        Assert.Equal(StatusCodes.Status202Accepted, existente.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(existente.Value), JsonSerializer.Serialize(inexistente.Value));
        Assert.DoesNotContain("token", JsonSerializer.Serialize(existente.Value), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueRestablecerCierreLaCookieSinIniciarOtraSesion()
    {
        //Arrange
        var controller = CrearController();
        var dto = new RestablecerContrasenaDto();
        _auth.Setup(a => a.RestablecerContrasenaAsync(dto, default)).Returns(Task.CompletedTask);

        //Act
        var respuesta = await controller.RestablecerContrasena(dto, default);

        //Assert
        Assert.IsType<NoContentResult>(respuesta);
        Assert.Contains(AuthController.RefreshCookieName + "=;", controller.Response.Headers.SetCookie.ToString());
        _auth.Verify(a => a.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueUnEnlaceInvalidoDevuelvaBadRequest()
    {
        //Arrange
        var controller = CrearController();
        _auth.Setup(a => a.RestablecerContrasenaAsync(It.IsAny<RestablecerContrasenaDto>(), default))
            .ThrowsAsync(new ArgumentException("El enlace no es válido, venció o ya fue utilizado."));

        //Act
        var respuesta = await controller.RestablecerContrasena(new(), default);

        //Assert
        Assert.IsType<BadRequestObjectResult>(respuesta);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueLaFuncionalidadNoDisponibleDevuelva503DesdeElMiddleware(bool restablecer)
    {
        //Arrange
        var controller = CrearController();
        var error = new RecuperacionContrasenaNoDisponibleException();
        _auth.Setup(a => a.SolicitarRecuperacionAsync(It.IsAny<RecuperarContrasenaDto>(), default))
            .ThrowsAsync(error);
        _auth.Setup(a => a.RestablecerContrasenaAsync(It.IsAny<RestablecerContrasenaDto>(), default))
            .ThrowsAsync(error);

        //Act
        var respuesta = await MiddlewareTestHelper.Ejecutar(controller.HttpContext,
            async () => restablecer
                ? await controller.RestablecerContrasena(new(), default)
                : await controller.RecuperarContrasena(new(), default));

        //Assert
        var resultado = Assert.IsType<ObjectResult>(respuesta);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, resultado.StatusCode);
        var problema = Assert.IsType<ProblemDetails>(resultado.Value);
        Assert.Equal(error.Message, problema.Detail);
        Assert.Contains("application/problem+json", resultado.ContentTypes);
        Assert.Equal(0, controller.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task QueEnProduccionSeExijaElEncabezadoDelFrontend()
    {
        //Arrange
        var controller = CrearController("Production");

        //Act
        var respuesta = await controller.RecuperarContrasena(new() { Email = "ana@example.com" }, default);

        //Assert
        Assert.IsType<BadRequestObjectResult>(respuesta);
        _auth.Verify(a => a.SolicitarRecuperacionAsync(It.IsAny<RecuperarContrasenaDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
