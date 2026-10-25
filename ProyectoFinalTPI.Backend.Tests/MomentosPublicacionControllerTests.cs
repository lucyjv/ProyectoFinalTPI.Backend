using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class MomentosPublicacionControllerTests
{
    private static MomentosPublicacionController Crear(Mock<IExploracionMomentosServicio> servicio, int? usuarioId = null) => new(servicio.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        {
            User = usuarioId == null ? new ClaimsPrincipal() : new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.Value.ToString()) }, "Test"))
        } }
    };

    [Theory]
    [InlineData(null)]
    [InlineData(7)]
    public async Task QueSePuedaLeerElEstadoConOSinSesion(int? usuarioId)
    {
        //Arrange
        var servicio = new Mock<IExploracionMomentosServicio>(MockBehavior.Strict);
        var estado = new MomentosPublicacionDto { PublicacionId = 10, VersionRelaciones = 3 };
        servicio.Setup(s => s.ObtenerEstado(10, usuarioId, CancellationToken.None)).ReturnsAsync(estado);
        var controller = Crear(servicio, usuarioId);

        //Act
        var result = await controller.Obtener(10, CancellationToken.None);

        //Assert
        Assert.Same(estado, Assert.IsType<OkObjectResult>(result.Result).Value);
        servicio.VerifyAll();
    }

    [Fact]
    public async Task QueLaCorreccionUseElAutorDelTokenYDevuelvaLaVersionActualizada()
    {
        //Arrange
        var servicio = new Mock<IExploracionMomentosServicio>(MockBehavior.Strict);
        var request = new CorregirMomentosRequest { MomentoIds = new() { 1, 2 }, PermitirMomentosAutomaticos = false, VersionRelaciones = 3 };
        var estado = new MomentosPublicacionDto { PublicacionId = 10, VersionRelaciones = 4 };
        using var cancelacion = new CancellationTokenSource();
        servicio.Setup(s => s.Corregir(10, 7, request.MomentoIds, false, 3, cancelacion.Token)).ReturnsAsync(estado);
        var controller = Crear(servicio, 7);

        //Act
        var result = await controller.Corregir(10, request, cancelacion.Token);

        //Assert
        var respuesta = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, respuesta.StatusCode);
        Assert.Same(estado, respuesta.Value);
        servicio.VerifyAll();
    }

    [Fact]
    public async Task QueNoSeCorrijaSinUnUsuarioAutenticado()
    {
        //Arrange
        var servicio = new Mock<IExploracionMomentosServicio>(MockBehavior.Strict);
        var controller = Crear(servicio);

        //Act
        var result = await controller.Corregir(10, new CorregirMomentosRequest(), CancellationToken.None);

        //Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
        servicio.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task QueLosErroresDeCorreccionSeDevuelvanComoProblemDetails(int codigo)
    {
        //Arrange
        var servicio = new Mock<IExploracionMomentosServicio>(MockBehavior.Strict);
        Exception error = codigo switch
        {
            400 => new ArgumentException("Selección inválida"),
            403 => new UnauthorizedAccessException("Solo el autor puede corregir"),
            404 => new KeyNotFoundException("No existe"),
            _ => new ConflictoRelacionesException()
        };
        var request = new CorregirMomentosRequest { MomentoIds = new(), PermitirMomentosAutomaticos = false, VersionRelaciones = 0 };
        servicio.Setup(s => s.Corregir(10, 7, request.MomentoIds, false, 0, CancellationToken.None)).ThrowsAsync(error);
        var controller = Crear(servicio, 7);

        //Act
        var result = await MiddlewareTestHelper.Ejecutar(controller.HttpContext,
            async () => (await controller.Corregir(10, request, CancellationToken.None)).Result);

        //Assert
        var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
        var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
        Assert.Equal(codigo, respuesta.StatusCode);
        Assert.Equal(codigo, problema.Status);
        Assert.Equal(error.Message, problema.Detail);
    }
}
