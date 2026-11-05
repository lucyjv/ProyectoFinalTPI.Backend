using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminComentariosControllerTests
{
    [Fact]
    public async Task QueEliminarDelegueYDevuelvaNoContent()
    {
        var servicio = new Mock<IAdminComentarioServicio>();
        var resultado = await new AdminComentariosController(servicio.Object).Eliminar(4, default);
        Assert.IsType<NoContentResult>(resultado);
        servicio.Verify(s => s.Eliminar(4, It.IsAny<CancellationToken>()), Times.Once);
    }
}
