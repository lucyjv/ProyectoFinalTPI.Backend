using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminPublicacionesControllerTests
{
    private readonly Mock<IAdminPublicacionServicio> _servicio = new();

    [Fact]
    public async Task QueListarDelegueFiltrosEnServicio()
    {
        var filtro = new FiltroPublicacionesAdminDto { Pagina = 2, Tamanio = 10 };
        var esperado = new PaginaAdminDto<PublicacionAdminDto> { Pagina = 2, Tamanio = 10 };
        _servicio.Setup(s => s.Listar(filtro, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);
        var resultado = await new AdminPublicacionesController(_servicio.Object).Listar(filtro, default);
        Assert.Same(esperado, resultado);
    }

    [Fact]
    public async Task QueOcultarDevuelvaNoContent()
    {
        var dto = new CambiarVisibilidadPublicacionDto { Ocultar = true, Motivo = "Normas" };
        var resultado = await new AdminPublicacionesController(_servicio.Object)
            .CambiarVisibilidad(7, dto, default);
        Assert.IsType<NoContentResult>(resultado);
        _servicio.Verify(s => s.CambiarVisibilidad(7, dto, It.IsAny<CancellationToken>()), Times.Once);
    }
}
