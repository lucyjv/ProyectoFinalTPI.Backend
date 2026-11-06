using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminUsuariosControllerTests
{
    [Fact]
    public async Task QueDarDeBajaUseElAdministradorAutenticado()
    {
        var servicio = new Mock<IAdminUsuarioServicio>();
        var controller = new AdminUsuariosController(servicio.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "9")], "Test"));
        var resultado = await controller.DarDeBaja(15, default);
        Assert.IsType<NoContentResult>(resultado);
        servicio.Verify(s => s.DarDeBaja(15, 9, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueListarDelegueEnServicio()
    {
        var servicio = new Mock<IAdminUsuarioServicio>();
        var filtro = new FiltroUsuariosAdminDto();
        servicio.Setup(s => s.Listar(filtro, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginaAdminDto<UsuarioAdminDto>());
        var resultado = await new AdminUsuariosController(servicio.Object).Listar(filtro, default);
        Assert.NotNull(resultado);
    }
}
