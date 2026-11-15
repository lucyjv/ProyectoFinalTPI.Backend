using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CategoriasCuentaControllerTests
{
    private readonly Mock<ICategoriaServicio> _categoriaServicioMock = new();

    #region Obtener Catalogo

    [Fact]
    public void QueObtenerCatalogoDevuelvaCatalogoCompleto()
    {
        //Arrange
        var catalogo = new List<CategoriaCuentaDto>
        {
            new("bronce", "Bronce", 3, false, true, "baja", Array.Empty<string>()),
            new("oro", "Oro", null, true, true, "alta", Array.Empty<string>())
        };

        _categoriaServicioMock.Setup(s => s.ObtenerCatalogo()).Returns(catalogo);
        var controller = CrearController(usuarioId: null);

        //Act
        var resultado = controller.Obtener();

        //Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        _categoriaServicioMock.Verify(s => s.ObtenerCatalogo(), Times.Once);
    }

    #endregion

    #region Obtener Estado

    [Fact]
    public async Task QueObtenerEstadoAutenticadoDevuelvaOkConEstado()
    {
        //Arrange
        const int usuarioId = 7;
        var estadoEsperado = new EstadoCategoriaDto(
            new CategoriaCuentaDto("plata", "Plata", 10, false, true, "media", Array.Empty<string>()),
            PublicacionesConsumidas: 2,
            PublicacionesRestantes: 8,
            SemanaInicio: new DateOnly(2026, 11, 9),
            ReinicioSemana: new DateOnly(2026, 11, 16),
            MaximoAdjuntos: 5);

        _categoriaServicioMock
            .Setup(s => s.ObtenerEstado(usuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(estadoEsperado);

        var controller = CrearController(usuarioId);

        //Act
        var actionResult = await controller.ObtenerEstado(CancellationToken.None);

        //Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var estado = Assert.IsType<EstadoCategoriaDto>(okResult.Value);
        Assert.Equal("plata", estado.Categoria.Codigo);
        Assert.Equal(8, estado.PublicacionesRestantes);
        _categoriaServicioMock.Verify(s => s.ObtenerEstado(usuarioId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueObtenerEstadoSinAutenticarDevuelvaUnauthorized()
    {
        //Arrange
        var controller = CrearController(usuarioId: null);

        //Act
        var actionResult = await controller.ObtenerEstado(CancellationToken.None);

        //Assert
        Assert.IsType<UnauthorizedResult>(actionResult.Result);
        _categoriaServicioMock.Verify(s => s.ObtenerEstado(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private CategoriasCuentaController CrearController(int? usuarioId)
    {
        var controller = new CategoriasCuentaController(_categoriaServicioMock.Object);
        var claims = new List<Claim>();

        if (usuarioId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, usuarioId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, usuarioId.HasValue ? "TestAuth" : null);
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    #endregion
}

