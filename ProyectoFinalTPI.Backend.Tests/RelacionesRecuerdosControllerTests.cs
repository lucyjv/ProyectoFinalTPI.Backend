using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class RelacionesRecuerdosControllerTests
{
    private readonly RelacionServicioFake _servicio = new();
    private readonly RelacionesRecuerdosController _controller;

    public RelacionesRecuerdosControllerTests()
    {
        _controller = new RelacionesRecuerdosController(_servicio)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task QueSeDevuelvaOkConLosBloquesDeRelacionesDelRecuerdo()
    {
        //Arrange
        _servicio.Relaciones = new RelacionesRecuerdoDto
        {
            PublicacionId = 10,
            Dia = new() { Items = new() { new RecuerdoRelacionadoDto { Id = 11, MismoDia = true } } },
            Lugar = new() { Items = new() { new RecuerdoRelacionadoDto { Id = 12 } } }
        };
        using var cancelacion = new CancellationTokenSource();

        //Act
        var result = await _controller.Obtener(25, cancelacion.Token);

        //Assert
        var respuesta = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, respuesta.StatusCode);
        var relaciones = Assert.IsType<RelacionesRecuerdoDto>(respuesta.Value);
        Assert.Same(_servicio.Relaciones, relaciones);
        Assert.Equal(10, relaciones.PublicacionId);
        Assert.Equal(11, Assert.Single(relaciones.Dia.Items).Id);
        Assert.Equal(12, Assert.Single(relaciones.Lugar.Items).Id);
        Assert.Equal(25, _servicio.IdConsultado);
        Assert.Equal(cancelacion.Token, _servicio.TokenRecibido);
    }

    [Theory]
    [InlineData("dia")]
    [InlineData("lugar")]
    public async Task QueSeDevuelvaOkConLaPaginaYSeEnvienLosFiltrosAlServicio(string tipo)
    {
        //Arrange
        var cursor = "cursor-recibido";
        _servicio.Pagina = new ResultadoPaginadoDto<RecuerdoRelacionadoDto>
        {
            Items = new() { new RecuerdoRelacionadoDto { Id = 11, DistanciaMetros = 125 } },
            SiguienteCursor = "pagina-siguiente"
        };
        using var cancelacion = new CancellationTokenSource();

        //Act
        var result = await _controller.Listar(10, tipo, cursor, 5, cancelacion.Token);

        //Assert
        var respuesta = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, respuesta.StatusCode);
        var pagina = Assert.IsType<ResultadoPaginadoDto<RecuerdoRelacionadoDto>>(respuesta.Value);
        Assert.Same(_servicio.Pagina, pagina);
        Assert.Equal(11, Assert.Single(pagina.Items).Id);
        Assert.Equal("pagina-siguiente", pagina.SiguienteCursor);
        Assert.True(pagina.HayMas);
        Assert.Equal(10, _servicio.IdConsultado);
        Assert.Equal(tipo, _servicio.TipoRecibido);
        Assert.Equal(cursor, _servicio.CursorRecibido);
        Assert.Equal(5, _servicio.LimiteRecibido);
        Assert.Equal(cancelacion.Token, _servicio.TokenRecibido);
    }

    [Fact]
    public async Task QueSeConsultePorLugarConLimiteVeinteSiNoSeIndicanFiltros()
    {
        //Arrange
        var id = 10;

        //Act
        var result = await _controller.Listar(id);

        //Assert
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(id, _servicio.IdConsultado);
        Assert.Equal("lugar", _servicio.TipoRecibido);
        Assert.Equal(20, _servicio.LimiteRecibido);
        Assert.Null(_servicio.CursorRecibido);
    }

    [Fact]
    public async Task QueSeDevuelvaUnaPaginaVaciaSinCursorCuandoNoHayCoincidencias()
    {
        //Arrange
        _servicio.Pagina = new ResultadoPaginadoDto<RecuerdoRelacionadoDto>();

        //Act
        var result = await _controller.Listar(10);

        //Assert
        var respuesta = Assert.IsType<OkObjectResult>(result.Result);
        var pagina = Assert.IsType<ResultadoPaginadoDto<RecuerdoRelacionadoDto>>(respuesta.Value);
        Assert.Empty(pagina.Items);
        Assert.Null(pagina.SiguienteCursor);
        Assert.False(pagina.HayMas);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueSeDevuelvaNotFoundSiElRecuerdoNoEstaDisponible(bool listado)
    {
        //Arrange
        _servicio.Error = new KeyNotFoundException("La publicación no existe o no está disponible.");

        //Act
        var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
            listado ? (await _controller.Listar(99)).Result : (await _controller.Obtener(99, CancellationToken.None)).Result);

        //Assert
        var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
        var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
        Assert.Equal(404, respuesta.StatusCode);
        Assert.Equal(404, problema.Status);
        Assert.Equal(_servicio.Error.Message, problema.Detail);
        Assert.Contains("application/problem+json", respuesta.ContentTypes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueSeDevuelvaBadRequestSiElServicioRechazaLosParametros(bool listado)
    {
        //Arrange
        _servicio.Error = new ArgumentException("El identificador debe ser positivo.");

        //Act
        var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
            listado ? (await _controller.Listar(0)).Result : (await _controller.Obtener(0, CancellationToken.None)).Result);

        //Assert
        var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
        var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
        Assert.Equal(400, respuesta.StatusCode);
        Assert.Equal(400, problema.Status);
        Assert.Equal(_servicio.Error.Message, problema.Detail);
    }

    [Fact]
    public async Task QueNoSeExponganDetallesInternosSiFallaLaConsulta()
    {
        //Arrange
        _servicio.Error = new InvalidOperationException("Detalle privado de conexión a la base");

        //Act
        var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext,
            async () => (await _controller.Listar(10)).Result);

        //Assert
        var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
        var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
        Assert.Equal(500, respuesta.StatusCode);
        Assert.Equal(500, problema.Status);
        Assert.NotNull(problema.Detail);
        Assert.DoesNotContain(_servicio.Error.Message, problema.Detail);
    }

    private sealed class RelacionServicioFake : IRelacionRecuerdosServicio
    {
        public RelacionesRecuerdoDto Relaciones = new();
        public ResultadoPaginadoDto<RecuerdoRelacionadoDto> Pagina = new();
        public Exception? Error;
        public int? IdConsultado;
        public string? TipoRecibido, CursorRecibido;
        public int? LimiteRecibido;
        public CancellationToken TokenRecibido;

        public Task<RelacionesRecuerdoDto> ObtenerRelaciones(int id, CancellationToken ct)
        {
            IdConsultado = id;
            TokenRecibido = ct;
            if (Error != null) throw Error;
            return Task.FromResult(Relaciones);
        }

        public Task<ResultadoPaginadoDto<RecuerdoRelacionadoDto>> ObtenerRelacionadas(int id, string tipo, string? cursor, int limite, CancellationToken ct)
        {
            IdConsultado = id;
            TipoRecibido = tipo;
            CursorRecibido = cursor;
            LimiteRecibido = limite;
            TokenRecibido = ct;
            if (Error != null) throw Error;
            return Task.FromResult(Pagina);
        }
    }
}
