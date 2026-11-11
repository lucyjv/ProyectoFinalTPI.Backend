using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminReportesControllerTests
{
    [Fact]
    public async Task QueListarDelegueLosFiltros()
    {
        var servicio = new Mock<IAdminReporteServicio>();
        var filtro = new FiltroReportesAdminDto { Estado = EstadoReporte.Pendiente };
        var esperado = new PaginaAdminDto<ReporteAdminDto> { TotalItems = 3 };
        servicio.Setup(s => s.Listar(filtro, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var resultado = await new AdminReportesController(servicio.Object).Listar(filtro, default);

        Assert.Same(esperado, resultado);
    }

    [Fact]
    public async Task QueResolverUseAdministradorAutenticado()
    {
        var servicio = new Mock<IAdminReporteServicio>();
        var dto = new ResolverReporteAdminDto
        {
            ReporteId = 5,
            Accion = AccionReporte.Desestimar
        };
        var controller = new AdminReportesController(servicio.Object)
        {
            ControllerContext = ContextoConUsuario(2)
        };

        var resultado = await controller.Resolver(dto, default);

        Assert.IsType<NoContentResult>(resultado);
        servicio.Verify(s => s.Resolver(2, dto, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ControllerContext ContextoConUsuario(int id) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"))
        }
    };
}
