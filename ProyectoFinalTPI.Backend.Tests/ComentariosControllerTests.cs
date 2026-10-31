using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Comentarios;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ComentariosControllerTests
{
    private readonly Mock<IComentarioServicio> _comentarioServicioMock = new();
    private readonly ComentariosController _controller;

    public ComentariosControllerTests()
    {
        _controller = new ComentariosController(_comentarioServicioMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    #region Obtener Comentarios y Respuestas

    [Fact]
    public async Task QueObtenerRaicesDelegueEnServicioYDevuelvaPaginacion()
    {
        //Arrange
        AutenticarComo(10);
        var esperado = new ResultadoPaginadoDto<ComentarioDto>
        {
            Items = new List<ComentarioDto> { CrearComentarioDto(1, 42, "Comentario 1") }
        };
        _comentarioServicioMock
            .Setup(s => s.ObtenerRaices(42, "cursor123", 20, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(esperado);

        //Act
        var resultado = await _controller.ObtenerRaices(42, "cursor123", 20);

        //Assert
        Assert.Same(esperado, resultado);
        _comentarioServicioMock.Verify(s => s.ObtenerRaices(42, "cursor123", 20, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueObtenerRespuestasDelegueEnServicioYDevuelvaPaginacion()
    {
        //Arrange
        var esperado = new ResultadoPaginadoDto<ComentarioDto>
        {
            Items = new List<ComentarioDto> { CrearComentarioDto(2, 42, "Respuesta 1") }
        };
        _comentarioServicioMock
            .Setup(s => s.ObtenerRespuestas(42, 1, null, 3, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(esperado);

        //Act
        var resultado = await _controller.ObtenerRespuestas(42, 1);

        //Assert
        Assert.Same(esperado, resultado);
        _comentarioServicioMock.Verify(s => s.ObtenerRespuestas(42, 1, null, 3, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Creación de Comentarios

    [Fact]
    public async Task QueCrearDevuelvaUnauthorizedSiNoEstaAutenticado()
    {
        //Arrange
        var request = new CrearComentarioRequest { Contenido = "Buen recuerdo!" };

        //Act
        var resultado = await _controller.Crear(42, request);

        //Assert
        Assert.IsType<UnauthorizedResult>(resultado);
        _comentarioServicioMock.Verify(s => s.Crear(It.IsAny<Comentario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueCrearDevuelvaCreatedConLocationYDtoSiEstaAutenticado()
    {
        //Arrange
        AutenticarComo(10);
        var request = new CrearComentarioRequest { Contenido = "Buen recuerdo!", ComentarioPadreId = 1 };
        var comentarioCreado = CrearComentarioDto(5, 42, "Buen recuerdo!");

        _comentarioServicioMock
            .Setup(s => s.Crear(It.Is<Comentario>(c => c.PublicacionId == 42 && c.UsuarioId == 10 && c.Contenido == "Buen recuerdo!"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comentarioCreado);

        //Act
        var resultado = await _controller.Crear(42, request);

        //Assert
        var created = Assert.IsType<CreatedResult>(resultado);
        Assert.Equal("/api/publicaciones/42/comentarios/5", created.Location);
        var dto = Assert.IsType<ComentarioDto>(created.Value);
        Assert.Equal(5, dto.Id);
        _comentarioServicioMock.Verify(s => s.Crear(It.IsAny<Comentario>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Eliminación de Comentarios

    [Fact]
    public async Task QueEliminarDevuelvaUnauthorizedSiNoEstaAutenticado()
    {
        //Act
        var resultado = await _controller.Eliminar(42, 5);

        //Assert
        Assert.IsType<UnauthorizedResult>(resultado);
        _comentarioServicioMock.Verify(s => s.Eliminar(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueEliminarDevuelvaNoContentSiEstaAutenticado()
    {
        //Arrange
        AutenticarComo(10);
        _comentarioServicioMock
            .Setup(s => s.Eliminar(42, 5, 10, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        //Act
        var resultado = await _controller.Eliminar(42, 5);

        //Assert
        Assert.IsType<NoContentResult>(resultado);
        _comentarioServicioMock.Verify(s => s.Eliminar(42, 5, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Reacciones a Comentarios

    [Fact]
    public async Task QueEstablecerReaccionDevuelvaUnauthorizedSiNoEstaAutenticado()
    {
        //Arrange
        var request = new ReaccionComentarioRequest { Tipo = "corazon" };

        //Act
        var resultado = await _controller.EstablecerReaccion(42, 5, request);

        //Assert
        Assert.IsType<UnauthorizedResult>(resultado.Result);
        _comentarioServicioMock.Verify(s => s.EstablecerReaccion(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueEstablecerReaccionDevuelvaOkConDtoSiEstaAutenticado()
    {
        //Arrange
        AutenticarComo(10);
        var request = new ReaccionComentarioRequest { Tipo = "corazon" };
        var comentarioActualizado = CrearComentarioDto(5, 42, "Contenido", "corazon");

        _comentarioServicioMock
            .Setup(s => s.EstablecerReaccion(42, 5, 10, "corazon", It.IsAny<CancellationToken>()))
            .ReturnsAsync(comentarioActualizado);

        //Act
        var resultado = await _controller.EstablecerReaccion(42, 5, request);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var dto = Assert.IsType<ComentarioDto>(ok.Value);
        Assert.Equal("corazon", dto.ReaccionActual);
        _comentarioServicioMock.Verify(s => s.EstablecerReaccion(42, 5, 10, "corazon", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueQuitarReaccionDevuelvaUnauthorizedSiNoEstaAutenticado()
    {
        //Act
        var resultado = await _controller.QuitarReaccion(42, 5);

        //Assert
        Assert.IsType<UnauthorizedResult>(resultado.Result);
        _comentarioServicioMock.Verify(s => s.QuitarReaccion(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueQuitarReaccionDevuelvaOkConDtoSiEstaAutenticado()
    {
        //Arrange
        AutenticarComo(10);
        var comentarioSinReaccion = CrearComentarioDto(5, 42, "Contenido", null);

        _comentarioServicioMock
            .Setup(s => s.QuitarReaccion(42, 5, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comentarioSinReaccion);

        //Act
        var resultado = await _controller.QuitarReaccion(42, 5);

        //Assert
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var dto = Assert.IsType<ComentarioDto>(ok.Value);
        Assert.Null(dto.ReaccionActual);
        _comentarioServicioMock.Verify(s => s.QuitarReaccion(42, 5, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helpers Privados

    private void AutenticarComo(int usuarioId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    private static ComentarioDto CrearComentarioDto(int id, int publicacionId, string contenido, string? reaccion = null) => new()
    {
        Id = id,
        PublicacionId = publicacionId,
        Contenido = contenido,
        FechaCreacion = DateTime.UtcNow,
        Autor = new AutorResumenDto { Id = 10, Username = "testUser" },
        ReaccionActual = reaccion
    };

    #endregion
}
