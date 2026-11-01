using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class RelacionesRecuerdosControllerTests
{
    private readonly Mock<IRelacionRecuerdosServicio> _servicioMock = new();
    private readonly RelacionesRecuerdosController _controller;

    public RelacionesRecuerdosControllerTests()
    {
        _controller = new RelacionesRecuerdosController(_servicioMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    #region Obtener Relaciones

    [Fact]
    public async Task QueSeDevuelvaOkConLosBloquesDeRelacionesDelRecuerdo()
    {
        //Arrange
        var relacionesEsperadas = new RelacionesRecuerdoDto
        {
            PublicacionId = 10,
            Dia = new() { Items = new() { new RecuerdoRelacionadoDto { Id = 11, MismoDia = true } } },
            Lugar = new() { Items = new() { new RecuerdoRelacionadoDto { Id = 12 } } }
        };
        using var cancelacion = new CancellationTokenSource();
        _servicioMock
            .Setup(s => s.ObtenerRelaciones(25, cancelacion.Token))
            .ReturnsAsync(relacionesEsperadas);

        //Act
        var result = await _controller.Obtener(25, cancelacion.Token);

        //Assert
        var respuesta = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, respuesta.StatusCode);
        var relaciones = Assert.IsType<RelacionesRecuerdoDto>(respuesta.Value);
        Assert.Same(relacionesEsperadas, relaciones);
        Assert.Equal(10, relaciones.PublicacionId);
        Assert.Equal(11, Assert.Single(relaciones.Dia.Items).Id);
        Assert.Equal(12, Assert.Single(relaciones.Lugar.Items).Id);
        _servicioMock.Verify(s => s.ObtenerRelaciones(25, cancelacion.Token), Times.Once);
    }

    #endregion

    #region Listar Relaciones Paginadas

    [Theory]
    [InlineData("dia")]
    [InlineData("lugar")]
    public async Task QueSeDevuelvaOkConLaPaginaYSeEnvienLosFiltrosAlServicio(string tipo)
    {
        //Arrange
        var cursor = "cursor-recibido";
        var paginaEsperada = new ResultadoPaginadoDto<RecuerdoRelacionadoDto>
        {
            Items = new() { new RecuerdoRelacionadoDto { Id = 11, DistanciaMetros = 125 } },
            SiguienteCursor = "pagina-siguiente"
        };
        using var cancelacion = new CancellationTokenSource();
        _servicioMock
            .Setup(s => s.ObtenerRelacionadas(10, tipo, cursor, 5, cancelacion.Token))
            .ReturnsAsync(paginaEsperada);

        //Act
        var result = await _controller.Listar(10, tipo, cursor, 5, cancelacion.Token);

        //Assert
        var respuesta = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, respuesta.StatusCode);
        var pagina = Assert.IsType<ResultadoPaginadoDto<RecuerdoRelacionadoDto>>(respuesta.Value);
        Assert.Same(paginaEsperada, pagina);
        Assert.Equal(11, Assert.Single(pagina.Items).Id);
        Assert.Equal("pagina-siguiente", pagina.SiguienteCursor);
        Assert.True(pagina.HayMas);
        _servicioMock.Verify(s => s.ObtenerRelacionadas(10, tipo, cursor, 5, cancelacion.Token), Times.Once);
    }

    [Fact]
    public async Task QueSeConsultePorLugarConLimiteVeinteSiNoSeIndicanFiltros()
    {
        //Arrange
        var id = 10;
        var paginaVacia = new ResultadoPaginadoDto<RecuerdoRelacionadoDto>();
        _servicioMock
            .Setup(s => s.ObtenerRelacionadas(id, "lugar", null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginaVacia);

        //Act
        var result = await _controller.Listar(id);

        //Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _servicioMock.Verify(s => s.ObtenerRelacionadas(id, "lugar", null, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueSeDevuelvaUnaPaginaVaciaSinCursorCuandoNoHayCoincidencias()
    {
        //Arrange
        var paginaVacia = new ResultadoPaginadoDto<RecuerdoRelacionadoDto>();
        _servicioMock
            .Setup(s => s.ObtenerRelacionadas(10, "lugar", null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginaVacia);

        //Act
        var result = await _controller.Listar(10);

        //Assert
        var respuesta = Assert.IsType<OkObjectResult>(result.Result);
        var pagina = Assert.IsType<ResultadoPaginadoDto<RecuerdoRelacionadoDto>>(respuesta.Value);
        Assert.Empty(pagina.Items);
        Assert.Null(pagina.SiguienteCursor);
        Assert.False(pagina.HayMas);
    }

    #endregion

    #region Manejo de Errores y Validaciones

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueSeDevuelvaNotFoundSiElRecuerdoNoEstaDisponible(bool listado)
    {
        //Arrange
        var error = new KeyNotFoundException("La publicación no existe o no está disponible.");
        _servicioMock
            .Setup(s => s.ObtenerRelaciones(99, It.IsAny<CancellationToken>()))
            .ThrowsAsync(error);
        _servicioMock
            .Setup(s => s.ObtenerRelacionadas(99, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(error);

        //Act
        var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
            listado ? (await _controller.Listar(99)).Result : (await _controller.Obtener(99, CancellationToken.None)).Result);

        //Assert
        var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
        var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
        Assert.Equal(404, respuesta.StatusCode);
        Assert.Equal(404, problema.Status);
        Assert.Equal(error.Message, problema.Detail);
        Assert.Contains("application/problem+json", respuesta.ContentTypes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueSeDevuelvaBadRequestSiElServicioRechazaLosParametros(bool listado)
    {
        //Arrange
        var error = new ArgumentException("El identificador debe ser positivo.");
        _servicioMock
            .Setup(s => s.ObtenerRelaciones(0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(error);
        _servicioMock
            .Setup(s => s.ObtenerRelacionadas(0, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(error);

        //Act
        var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
            listado ? (await _controller.Listar(0)).Result : (await _controller.Obtener(0, CancellationToken.None)).Result);

        //Assert
        var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
        var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
        Assert.Equal(400, respuesta.StatusCode);
        Assert.Equal(400, problema.Status);
        Assert.Equal(error.Message, problema.Detail);
    }

    [Fact]
    public async Task QueNoSeExponganDetallesInternosSiFallaLaConsulta()
    {
        //Arrange
        var error = new InvalidOperationException("Detalle privado de conexión a la base");
        _servicioMock
            .Setup(s => s.ObtenerRelacionadas(10, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(error);

        //Act
        var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext,
            async () => (await _controller.Listar(10)).Result);

        //Assert
        var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
        var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
        Assert.Equal(500, respuesta.StatusCode);
        Assert.Equal(500, problema.Status);
        Assert.NotNull(problema.Detail);
        Assert.DoesNotContain(error.Message, problema.Detail);
    }

    #endregion
}
