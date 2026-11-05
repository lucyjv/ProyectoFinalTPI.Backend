using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminEstadisticasControllerTests
{
    [Fact]
    public async Task QueObtenerDelegueElLimiteEnServicio()
    {
        var servicio = new Mock<IAdminEstadisticaServicio>();
        var esperado = new EstadisticasAdminDto { TotalUsuarios = 12 };
        servicio.Setup(s => s.Obtener(5, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);
        var resultado = await new AdminEstadisticasController(servicio.Object).Obtener(5);
        Assert.Same(esperado, resultado);
    }
}
