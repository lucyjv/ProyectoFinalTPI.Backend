using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class EtiquetadoPublicacionControllerTests
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly Mock<IPublicacionServicio> _publicacionServicioMock = new();
    private readonly Mock<IEtiquetadoPublicacionServicio> _etiquetadoServicioMock = new();

    #region Etiquetado Exitoso

    [Fact]
    public async Task QueEtiquetarUsuariosNuevosDevuelvaCreatedYCantidadCreada()
    {
        //Arrange
        var controller = CrearController();
        _etiquetadoServicioMock
            .Setup(s => s.EtiquetarUsuarios(25, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var request = new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10, 20 } };

        //Act
        var resultado = await MiddlewareTestHelper.Ejecutar(controller.HttpContext, async () =>
            await controller.EtiquetarUsuarios(25, request, CancellationToken.None));

        //Assert
        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal((int)HttpStatusCode.Created, respuesta.StatusCode);
        var contenido = JsonSerializer.Serialize(respuesta.Value, OpcionesJson);
        Assert.Contains("Se etiquetaron los usuarios", contenido);
        Assert.Contains("\"cantidadEtiquetasNuevas\":2", contenido);
        _etiquetadoServicioMock.Verify(s => s.EtiquetarUsuarios(
            25,
            It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 10, 20 })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueEtiquetasExistentesDevuelvanOk()
    {
        //Arrange
        var controller = CrearController();
        _etiquetadoServicioMock
            .Setup(s => s.EtiquetarUsuarios(25, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var request = new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10 } };

        //Act
        var resultado = await MiddlewareTestHelper.Ejecutar(controller.HttpContext, async () =>
            await controller.EtiquetarUsuarios(25, request, CancellationToken.None));

        //Assert
        var respuesta = Assert.IsType<OkObjectResult>(resultado);
        var contenido = JsonSerializer.Serialize(respuesta.Value, OpcionesJson);
        Assert.Equal((int)HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("usuarios ya etiquetados en la publicacion", contenido);
        Assert.Contains("\"cantidadEtiquetasNuevas\":0", contenido);
        _etiquetadoServicioMock.Verify(s => s.EtiquetarUsuarios(
            25,
            It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Validaciones y Manejo de Errores

    [Theory]
    [InlineData("Debe indicar al menos un usuario para etiquetar", 25, new int[] { })]
    [InlineData("Los IDs de usuarios deben ser mayores que cero.", 25, new int[] { 0 })]
    [InlineData("El ID de la publicación debe ser mayor que cero", 0, new int[] { 10 })]
    public async Task QueEntradaInvalidaDevuelvaBadRequest(
        string mensaje,
        int publicacionId,
        int[] usuariosIds)
    {
        //Arrange
        var controller = CrearController();
        _etiquetadoServicioMock
            .Setup(s => s.EtiquetarUsuarios(publicacionId, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException(mensaje));
        var request = new EtiquetarUsuariosRequest { UsuariosIds = usuariosIds.ToList() };

        //Act
        var resultado = await MiddlewareTestHelper.Ejecutar(controller.HttpContext, async () =>
            await controller.EtiquetarUsuarios(publicacionId, request, CancellationToken.None));

        //Assert
        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.IsType<ProblemDetails>(respuesta.Value);
        Assert.Equal((int)HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(mensaje, JsonSerializer.Serialize(respuesta.Value, OpcionesJson));
        _etiquetadoServicioMock.Verify(s => s.EtiquetarUsuarios(
            publicacionId,
            It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("La publicación no existe o no está disponible.")]
    [InlineData("No se encontraron los usuarios con ID: 99.")]
    public async Task QueEntidadInexistenteDevuelvaNotFoundConMensaje(string mensaje)
    {
        //Arrange
        var controller = CrearController();
        _etiquetadoServicioMock
            .Setup(s => s.EtiquetarUsuarios(25, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException(mensaje));
        var request = new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10 } };

        //Act
        var resultado = await MiddlewareTestHelper.Ejecutar(controller.HttpContext, async () =>
            await controller.EtiquetarUsuarios(25, request, CancellationToken.None));

        //Assert
        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.IsType<ProblemDetails>(respuesta.Value);
        Assert.Equal((int)HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains(mensaje, JsonSerializer.Serialize(respuesta.Value, OpcionesJson));
    }

    [Fact]
    public async Task QueErrorInesperadoDevuelva500SinExponerDetalles()
    {
        //Arrange
        var controller = CrearController();
        _etiquetadoServicioMock
            .Setup(s => s.EtiquetarUsuarios(25, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("detalle interno"));
        var request = new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10 } };

        //Act
        var resultado = await MiddlewareTestHelper.Ejecutar(controller.HttpContext, async () =>
            await controller.EtiquetarUsuarios(25, request, CancellationToken.None));

        //Assert
        var respuesta = Assert.IsType<ObjectResult>(resultado);
        var contenido = JsonSerializer.Serialize(respuesta.Value, OpcionesJson);
        Assert.Equal((int)HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Contains("No se pudo completar la operación", contenido);
        Assert.DoesNotContain("detalle interno", contenido);
    }

    #endregion

    #region Helpers Privados

    private PublicacionesController CrearController()
    {
        return new PublicacionesController(
            _publicacionServicioMock.Object,
            _etiquetadoServicioMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    #endregion
}
