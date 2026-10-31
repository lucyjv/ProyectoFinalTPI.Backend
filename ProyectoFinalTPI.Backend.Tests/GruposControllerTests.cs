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
    private readonly Mock<IGrupoServicio> _grupoServicioMock = new();

    #region Creación de Grupos

    [Fact]
    public async Task QueCrearDevuelvaCreatedYUsaUsuarioAutenticado()
    {
        //Arrange
        _grupoServicioMock
            .Setup(actual => actual.Crear(It.IsAny<Grupo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrupoResumenDto { Id = 12, Nombre = "Amigos" });
        var controller = CrearController(8);

        //Act
        var resultado = await controller.Crear(new CrearGrupoRequest
        {
            Nombre = "Amigos",
            Descripcion = "Nuestros recuerdos",
            Decada = 1990
        });

        //Assert
        var creado = Assert.IsType<CreatedResult>(resultado);
        Assert.Equal("/api/grupos/12", creado.Location);
        _grupoServicioMock.Verify(actual => actual.Crear(
            It.Is<Grupo>(grupo => grupo.CreadorId == 8),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueCrearSinAutenticacionDevuelvaUnauthorizedYNoLlameAlServicio()
    {
        //Arrange
        var controller = CrearController(null);

        //Act
        var resultado = await controller.Crear(new CrearGrupoRequest { Nombre = "Amigos", Decada = 1990 });

        //Assert
        Assert.IsType<UnauthorizedResult>(resultado);
        _grupoServicioMock.Verify(actual => actual.Crear(
            It.IsAny<Grupo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Membresía y Expulsión

    [Fact]
    public async Task QueUnirseUseElIdDelTokenSinRecibirUsuarioEnElCuerpo()
    {
        //Arrange
        var controller = CrearController(21);

        //Act
        var resultado = await controller.Unirse(4);

        //Assert
        Assert.IsType<NoContentResult>(resultado);
        _grupoServicioMock.Verify(actual => actual.Unirse(
            4, 21, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueExpulsarEnvieCreadorAutenticadoEIntegranteDeLaRuta()
    {
        //Arrange
        var controller = CrearController(10);

        //Act
        var resultado = await controller.Expulsar(4, 33);

        //Assert
        Assert.IsType<NoContentResult>(resultado);
        _grupoServicioMock.Verify(actual => actual.Expulsar(
            4, 10, 33, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Listado y Búsqueda

    [Fact]
    public async Task QueListadoPublicoPermitaUsuarioAnonimo()
    {
        //Arrange
        _grupoServicioMock
            .Setup(actual => actual.ObtenerTodos(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GrupoResumenDto { Id = 1, Nombre = "Publico" }]);
        var controller = CrearController(null);

        //Act
        var resultado = await controller.ObtenerTodos();

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var grupos = Assert.IsAssignableFrom<IReadOnlyList<GrupoResumenDto>>(ok.Value);
        Assert.Single(grupos);
        _grupoServicioMock.Verify(actual => actual.ObtenerTodos(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueBuscarSeaPublicoYEnvieTextoLimiteYUsuarioAlServicio()
    {
        //Arrange
        _grupoServicioMock
            .Setup(actual => actual.Buscar("viajes", 10, 21, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GrupoResumenDto { Id = 3, Nombre = "Viajes" }]);
        var controller = CrearController(21);

        //Act
        var resultado = await controller.Buscar(new BuscarGruposRequest
        {
            Texto = "viajes",
            Limite = 10
        });

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var grupos = Assert.IsAssignableFrom<IReadOnlyList<GrupoResumenDto>>(ok.Value);
        Assert.Single(grupos);
        _grupoServicioMock.Verify(actual => actual.Buscar(
            "viajes", 10, 21, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helpers Privados

    private GruposController CrearController(int? usuarioId)
    {
        var claims = usuarioId.HasValue
            ? new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.Value.ToString()) }
            : Array.Empty<Claim>();
        return new GruposController(_grupoServicioMock.Object)
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
    }

    #endregion
}
