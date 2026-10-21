using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class GestionDuplicadosMomentosControllerTests
{
    private readonly Mock<IGestionDuplicadosMomentoServicio> _servicio = new(MockBehavior.Strict);

    [Fact]
    public async Task QueLaBusquedaDevuelvaLasSugerenciasDelServicio()
    {
        //Arrange
        IReadOnlyList<CandidatoDuplicadoMomentoDto> esperado = [new(new(2, "Argentina campeón 1986", null, null, 1), 0.9, "textoSimilar")];
        _servicio.Setup(s => s.BuscarCandidatos(1, 10, CancellationToken.None)).ReturnsAsync(esperado);
        var controller = new GestionDuplicadosMomentosController(_servicio.Object);

        //Act
        var result = await controller.Buscar(1, CancellationToken.None, 10);

        //Assert
        Assert.Same(esperado, Assert.IsType<OkObjectResult>(result.Result).Value);
        _servicio.VerifyAll();
    }

    [Fact]
    public async Task QueLaVistaPreviaDevuelvaElImpactoSinFusionar()
    {
        //Arrange
        var esperado = new VistaFusionMomentoDto(new(1, "México 86", null, null, 1), new(2, "Argentina campeón 1986", null, null, 1), 3, 0, 0, false);
        _servicio.Setup(s => s.Previsualizar(1, 2, CancellationToken.None)).ReturnsAsync(esperado);
        var controller = new GestionDuplicadosMomentosController(_servicio.Object);

        //Act
        var result = await controller.Previsualizar(1, 2, CancellationToken.None);

        //Assert
        Assert.Same(esperado, Assert.IsType<OkObjectResult>(result.Result).Value);
        _servicio.VerifyAll();
    }

    [Fact]
    public async Task QueLaFusionUseElAdministradorAutenticadoYDevuelvaElCanonico()
    {
        //Arrange
        var solicitud = new SolicitudFusionMomentoDto(2, 1, 1);
        var esperado = new ResultadoFusionMomentoDto(1, 2, 3, 1, 2, false);
        _servicio.Setup(s => s.Fusionar(1, solicitud, 7, CancellationToken.None)).ReturnsAsync(esperado);
        var controller = new GestionDuplicadosMomentosController(_servicio.Object)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
                { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "7"), new Claim("esAdmin", "true")], "test")) } }
        };

        //Act
        var result = await controller.Fusionar(1, solicitud, CancellationToken.None);

        //Assert
        Assert.Same(esperado, Assert.IsType<OkObjectResult>(result.Result).Value);
        _servicio.VerifyAll();
    }
}
