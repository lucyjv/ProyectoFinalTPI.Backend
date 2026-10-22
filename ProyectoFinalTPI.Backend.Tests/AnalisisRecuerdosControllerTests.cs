using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AnalisisRecuerdosControllerTests
{
    [Fact]
    public async Task QueSeDevuelvaElEstadoPublicoSinDetallesInternos()
    {
        //Arrange
        var servicio = new Mock<IAnalisisRecuerdosServicio>(MockBehavior.Strict);
        var estado = new EstadoAnalisisRecuerdoDto { PublicacionId = 10, VersionContenido = 1, Estado = "pendiente" };
        using var cancelacion = new CancellationTokenSource();
        servicio.Setup(s => s.ObtenerEstado(10, cancelacion.Token)).ReturnsAsync(estado);
        var controller = new AnalisisRecuerdosController(servicio.Object);

        //Act
        var result = await controller.Obtener(10, cancelacion.Token);

        //Assert
        Assert.Same(estado, Assert.IsType<OkObjectResult>(result.Result).Value);
        servicio.VerifyAll();
    }
}
