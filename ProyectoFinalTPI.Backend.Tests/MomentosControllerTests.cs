using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class MomentosControllerTests
{
    [Fact]
    public async Task QueLaBusquedaDevuelvaLaPaginaYEnvieLosFiltrosAlServicio()
    {
        //Arrange
        var servicio = new Mock<IExploracionMomentosServicio>(MockBehavior.Strict);
        var pagina = new ResultadoPaginadoDto<MomentoResumenDto> { Items = new() { new() { Id = 1, Nombre = "Mundial" } } };
        using var cancelacion = new CancellationTokenSource();
        servicio.Setup(s => s.Buscar("Mundial", "cursor", 5, cancelacion.Token)).ReturnsAsync(pagina);
        var controller = new MomentosController(servicio.Object);

        //Act
        var result = await controller.Buscar("Mundial", "cursor", 5, cancelacion.Token);

        //Assert
        var respuesta = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, respuesta.StatusCode);
        Assert.Same(pagina, respuesta.Value);
        servicio.VerifyAll();
    }

    [Fact]
    public async Task QueSeBusqueSinFiltrosConLimiteVeintePorDefecto()
    {
        //Arrange
        var servicio = new Mock<IExploracionMomentosServicio>(MockBehavior.Strict);
        servicio.Setup(s => s.Buscar(null, null, 20, CancellationToken.None)).ReturnsAsync(new ResultadoPaginadoDto<MomentoResumenDto>());
        var controller = new MomentosController(servicio.Object);

        //Act
        var result = await controller.Buscar();

        //Assert
        Assert.IsType<OkObjectResult>(result.Result);
        servicio.VerifyAll();
    }

    [Fact]
    public async Task QueElDetalleDevuelvaElMomentoConSuJerarquia()
    {
        //Arrange
        var servicio = new Mock<IExploracionMomentosServicio>(MockBehavior.Strict);
        var detalle = new MomentoDetalleDto { Id = 2, Padre = new() { Id = 1 }, Hijos = new() { new() { Id = 3 } } };
        servicio.Setup(s => s.ObtenerDetalle(2, CancellationToken.None)).ReturnsAsync(detalle);
        var controller = new MomentosController(servicio.Object);

        //Act
        var result = await controller.Obtener(2, CancellationToken.None);

        //Assert
        var respuesta = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(detalle, respuesta.Value);
        Assert.Equal(1, detalle.Padre.Id);
        servicio.VerifyAll();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueSeEnvieLaOpcionDeDescendientesAlListarRecuerdos(bool descendientes)
    {
        //Arrange
        var servicio = new Mock<IExploracionMomentosServicio>(MockBehavior.Strict);
        var pagina = new ResultadoPaginadoDto<PublicacionResumenDto>();
        servicio.Setup(s => s.ObtenerPublicaciones(1, descendientes, "cursor", 5, CancellationToken.None)).ReturnsAsync(pagina);
        var controller = new MomentosController(servicio.Object);

        //Act
        var result = await controller.Publicaciones(1, descendientes, "cursor", 5);

        //Assert
        Assert.Same(pagina, Assert.IsType<OkObjectResult>(result.Result).Value);
        servicio.VerifyAll();
    }
}
