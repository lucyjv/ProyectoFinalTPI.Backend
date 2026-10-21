using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ReprocesamientoRecuerdosControllerTests
{
    private readonly Mock<IReprocesamientoRecuerdosServicio> _servicio = new(MockBehavior.Strict);

    [Fact]
    public async Task QueLaVistaPreviaDevuelvaLosCandidatosSinEncolarlos()
    {
        //Arrange
        var vista = new VistaPreviaReprocesamientoDto([new(10, 1, "Recuerdo", "sinAnalizador")], false, null);
        using var cancelacion = new CancellationTokenSource();
        _servicio.Setup(s => s.Previsualizar(5, 20, true, cancelacion.Token)).ReturnsAsync(vista);
        var controller = new ReprocesamientoRecuerdosController(_servicio.Object);

        //Act
        var result = await controller.Previsualizar(cancelacion.Token, 5, 20, true);

        //Assert
        Assert.Same(vista, Assert.IsType<OkObjectResult>(result.Result).Value);
        _servicio.VerifyAll();
    }

    [Fact]
    public async Task QueEncolarDevuelvaAcceptedYUseElAdministradorDelToken()
    {
        //Arrange
        var solicitud = new SolicitudReprocesamientoDto([new(10, 1)]);
        var resultado = new ResultadoReprocesamientoDto([new(10, 1, "encolado")]);
        _servicio.Setup(s => s.Encolar(solicitud, 7, CancellationToken.None)).ReturnsAsync(resultado);
        var controller = new ReprocesamientoRecuerdosController(_servicio.Object)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
                { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "7"), new Claim("esAdmin", "true")], "test")) } }
        };

        //Act
        var result = await controller.Encolar(solicitud, CancellationToken.None);

        //Assert
        Assert.Same(resultado, Assert.IsType<AcceptedResult>(result.Result).Value);
        _servicio.VerifyAll();
    }
}
