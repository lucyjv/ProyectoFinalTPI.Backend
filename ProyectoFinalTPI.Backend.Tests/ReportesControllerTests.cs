using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Reportes;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ReportesControllerTests
{
    [Fact]
    public async Task QueCrearUseUsuarioAutenticadoYDevuelvaCreated()
    {
        var servicio = new Mock<IReporteServicio>();
        var dto = new CrearReporteDto
        {
            TipoContenido = TipoContenidoReporte.Publicacion,
            ContenidoId = 8,
            Motivo = MotivoReporte.Spam
        };
        servicio.Setup(s => s.Crear(4, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Reporte { Id = 12 });
        var controller = new ReportesController(servicio.Object)
        {
            ControllerContext = ContextoConUsuario(4)
        };

        var resultado = await controller.Crear(dto, default);

        var created = Assert.IsType<CreatedResult>(resultado.Result);
        Assert.Equal(12, Assert.IsType<ReporteCreadoDto>(created.Value).Id);
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
