using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Grupo;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Grupos;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class GruposControllerTests
{
    [Fact]
    public async Task CrearDevuelveCreatedYUsaElUsuarioAutenticado()
    {
        var servicio = new Mock<IGrupoServicio>();
        servicio.Setup(actual => actual.Crear(
                It.IsAny<Grupo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrupoResumenDto { Id = 12, Nombre = "Amigos" });
        var controller = CrearController(servicio, 8);

        var resultado = await controller.Crear(new CrearGrupoRequest
        {
            Nombre = "Amigos",
            Descripcion = "Nuestros recuerdos",
            Decada = 1990
        });

        var creado = Assert.IsType<CreatedResult>(resultado);
        Assert.Equal("/api/grupos/12", creado.Location);
        servicio.Verify(actual => actual.Crear(
            It.Is<Grupo>(grupo => grupo.CreadorId == 8),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CrearSinAutenticacionDevuelveUnauthorizedYNoLlamaAlServicio()
    {
        var servicio = new Mock<IGrupoServicio>();
        var controller = CrearController(servicio);

        var resultado = await controller.Crear(new CrearGrupoRequest { Nombre = "Amigos", Decada = 1990 });

        Assert.IsType<UnauthorizedResult>(resultado);
        servicio.Verify(actual => actual.Crear(
            It.IsAny<Grupo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnirseUsaElIdDelTokenSinRecibirUsuarioEnElCuerpo()
    {
        var servicio = new Mock<IGrupoServicio>();
        var controller = CrearController(servicio, 21);

        var resultado = await controller.Unirse(4);

        Assert.IsType<NoContentResult>(resultado);
        servicio.Verify(actual => actual.Unirse(
            4, 21, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpulsarEnviaCreadorAutenticadoEIntegranteDeLaRuta()
    {
        var servicio = new Mock<IGrupoServicio>();
        var controller = CrearController(servicio, 10);

        var resultado = await controller.Expulsar(4, 33);

        Assert.IsType<NoContentResult>(resultado);
        servicio.Verify(actual => actual.Expulsar(
            4, 10, 33, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListadoPublicoPermiteUsuarioAnonimo()
    {
        var servicio = new Mock<IGrupoServicio>();
        servicio.Setup(actual => actual.ObtenerTodos(
                null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GrupoResumenDto { Id = 1, Nombre = "Publico" }]);
        var controller = CrearController(servicio);

        var resultado = await controller.ObtenerTodos();

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var grupos = Assert.IsAssignableFrom<IReadOnlyList<GrupoResumenDto>>(ok.Value);
        Assert.Single(grupos);
    }

    [Fact]
    public async Task BuscarEsPublicoYEnviaTextoLimiteYUsuarioAlServicio()
    {
        var servicio = new Mock<IGrupoServicio>();
        servicio.Setup(actual => actual.Buscar(
                "viajes", 10, 21, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GrupoResumenDto { Id = 3, Nombre = "Viajes" }]);
        var controller = CrearController(servicio, 21);

        var resultado = await controller.Buscar(new BuscarGruposRequest
        {
            Texto = "viajes",
            Limite = 10
        });

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var grupos = Assert.IsAssignableFrom<IReadOnlyList<GrupoResumenDto>>(ok.Value);
        Assert.Single(grupos);
        servicio.Verify(actual => actual.Buscar(
            "viajes", 10, 21, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GruposController CrearController(Mock<IGrupoServicio> servicio, int? usuarioId = null)
    {
        var claims = usuarioId.HasValue
            ? new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.Value.ToString()) }
            : Array.Empty<Claim>();
        var controller = new GruposController(servicio.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims,
                        usuarioId.HasValue ? "Prueba" : null))
                }
            }
        };
        return controller;
    }
}
