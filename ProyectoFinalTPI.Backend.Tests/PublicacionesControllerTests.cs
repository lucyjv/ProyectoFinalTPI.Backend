using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests
{
    public class PublicacionesControllerTests
    {
        private readonly Mock<IPublicacionServicio> _publicacionServicioMock = new();
        private readonly Mock<IEtiquetadoPublicacionServicio> _etiquetadoServicioMock = new();
        private readonly PublicacionesController _controller;

        public PublicacionesControllerTests()
        {
            _controller = new PublicacionesController(
                _publicacionServicioMock.Object,
                _etiquetadoServicioMock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        #region Helpers Reutilizables

        private static CrearPublicacionRequest CrearRequestEjemplo(int autorId = 10, string? url = "https://example.com/foto.jpg") => new()
        {
            Titulo = "Recuerdo",
            Descripcion = "Una visita",
            AutorId = autorId,
            NombreLugar = "Obelisco",
            Latitud = -34.6,
            Longitud = -58.3,
            Categoria = CategoriaEnum.Lugares,
            TipoMultimedia = MultimediaEnum.Foto,
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UrlArchivo = url
        };

        private class ArchivoConError : IFormFile
        {
            public string ContentType => "image/jpeg";
            public string ContentDisposition => string.Empty;
            public IHeaderDictionary Headers => new HeaderDictionary();
            public long Length => 3;
            public string Name => "Archivo";
            public string FileName => "foto.jpg";
            public Stream OpenReadStream() => throw new IOException("ruta interna");
            public void CopyTo(Stream target) => throw new NotImplementedException();
            public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        #endregion

        #region Creación de Publicaciones

        [Fact]
        public async Task QueSeDevuelvaCreatedConElIdYLaUbicacionDeLaPublicacionCreada()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.Crear(It.IsAny<Publicacion>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<NetTopologySuite.Geometries.Point?>()))
                .ReturnsAsync(25);
            var request = new CrearPublicacionRequest();

            //Act
            var result = await _controller.Crear(request);

            //Assert
            var respuesta = Assert.IsType<CreatedResult>(result);
            Assert.Equal(201, respuesta.StatusCode);
            Assert.Equal("/api/publicaciones/25", respuesta.Location);
            Assert.Equal(25, JsonSerializer.SerializeToElement(respuesta.Value).GetProperty("id").GetInt32());
        }

        [Fact]
        public async Task QueSeEnvienAlServicioLosDatosDeLaPublicacionConUrl()
        {
            //Arrange
            Publicacion? enviada = null;
            Stream? archivoEnviado = null;
            _publicacionServicioMock
                .Setup(s => s.Crear(It.IsAny<Publicacion>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<NetTopologySuite.Geometries.Point?>()))
                .Callback<Publicacion, Stream?, string?, NetTopologySuite.Geometries.Point?>((p, stream, _, _) =>
                {
                    enviada = p;
                    archivoEnviado = stream;
                })
                .ReturnsAsync(1);

            var request = CrearRequestEjemplo(10, "https://example.com/foto.jpg");

            //Act
            await _controller.Crear(request);

            //Assert
            Assert.NotNull(enviada);
            Assert.Equal("Recuerdo", enviada.Titulo);
            Assert.Equal("Obelisco", enviada.Lugar?.Nombre);
            Assert.Equal(10, enviada.UsuarioId);
            Assert.Equal(request.UrlArchivo, enviada.UrlMultimedia);
            Assert.Null(archivoEnviado);
        }

        [Fact]
        public async Task QueSeEnvienAlServicioElArchivoYSuNombre()
        {
            //Arrange
            string? nombreRecibido = null;
            byte[]? bytesRecibidos = null;
            _publicacionServicioMock
                .Setup(s => s.Crear(It.IsAny<Publicacion>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<NetTopologySuite.Geometries.Point?>()))
                .Callback<Publicacion, Stream?, string?, NetTopologySuite.Geometries.Point?>((_, stream, nombre, _) =>
                {
                    nombreRecibido = nombre;
                    if (stream != null)
                    {
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        bytesRecibidos = ms.ToArray();
                    }
                })
                .ReturnsAsync(1);

            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var request = new CrearPublicacionRequest
            {
                Archivo = new FormFile(stream, 0, stream.Length, "Archivo", "foto.jpg")
            };

            //Act
            await _controller.Crear(request);

            //Assert
            Assert.Equal("foto.jpg", nombreRecibido);
            Assert.Equal(new byte[] { 1, 2, 3 }, bytesRecibidos);
        }

        [Fact]
        public async Task QueSeDevuelvaNotFoundAlCrearSiElAutorNoExiste()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.Crear(It.IsAny<Publicacion>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<NetTopologySuite.Geometries.Point?>()))
                .ThrowsAsync(new KeyNotFoundException("El usuario indicado no existe."));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                await _controller.Crear(new CrearPublicacionRequest()));

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(404, respuesta.StatusCode);
            Assert.Equal(404, problema.Status);
            Assert.Equal("El usuario indicado no existe.", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaBadRequestSiElServicioRechazaLaCreacion()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.Crear(It.IsAny<Publicacion>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<NetTopologySuite.Geometries.Point?>()))
                .ThrowsAsync(new ArgumentException("Datos inválidos"));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                await _controller.Crear(new CrearPublicacionRequest()));

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(400, respuesta.StatusCode);
            Assert.Equal("Datos inválidos", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaErrorInternoSiNoSePuedeAbrirElArchivo()
        {
            //Arrange
            var request = new CrearPublicacionRequest { Archivo = new ArchivoConError() };
            _controller.HttpContext.TraceIdentifier = "prueba-archivo";
            _controller.HttpContext.Request.Path = "/api/publicaciones";

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                await _controller.Crear(request));

            //Assert
            var respuesta = Assert.IsType<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(500, respuesta.StatusCode);
            Assert.Equal("prueba-archivo", ((JsonElement)problema.Extensions["traceId"]!).GetString());
            Assert.Equal("/api/publicaciones", problema.Instance);
            Assert.Contains("application/problem+json", respuesta.ContentTypes);
            Assert.DoesNotContain("ruta interna", JsonSerializer.Serialize(problema));
            _publicacionServicioMock.Verify(s => s.Crear(It.IsAny<Publicacion>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<NetTopologySuite.Geometries.Point?>()), Times.Never);
        }

        #endregion

        #region Eliminación de Publicaciones

        [Fact]
        public async Task QueSeDevuelvaOkAlEliminarPublicacion()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.Eliminar(5, 10, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            //Act
            var result = await _controller.Eliminar(5, 10);

            //Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, ok.StatusCode);
            _publicacionServicioMock.Verify(s => s.Eliminar(5, 10, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueSeDevuelvaNotFoundAlEliminarPublicacionInexistente()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.Eliminar(99, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("No existe"));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                await _controller.Eliminar(99));

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(404, respuesta.StatusCode);
        }

        #endregion

        #region Pines y Mapa

        [Fact]
        public async Task QueSeDevuelvaOkConLosPinesDelMapa()
        {
            //Arrange
            var mapaEsperado = new ResultadoMapaDto
            {
                Pines = new List<PublicacionPinDto> { new() { Id = 5 } },
                HayMas = true
            };
            _publicacionServicioMock
                .Setup(s => s.ObtenerPines(It.IsAny<PublicacionFiltroDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapaEsperado);

            //Act
            var result = await _controller.ObtenerMapa(new PublicacionFiltroDto(), default);

            //Assert
            var respuesta = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(mapaEsperado, respuesta.Value);
        }

        [Fact]
        public async Task QueSeEnvieAlServicioElFiltroDelMapa()
        {
            //Arrange
            var filtro = new PublicacionFiltroDto
            {
                MinLatitud = -35,
                MaxLatitud = -34,
                MinLongitud = -59,
                MaxLongitud = -58,
                Categoria = CategoriaEnum.Lugares
            };
            PublicacionFiltroDto? filtroRecibido = null;
            _publicacionServicioMock
                .Setup(s => s.ObtenerPines(It.IsAny<PublicacionFiltroDto>(), It.IsAny<CancellationToken>()))
                .Callback<PublicacionFiltroDto, CancellationToken>((f, _) => filtroRecibido = f)
                .ReturnsAsync(new ResultadoMapaDto());

            //Act
            await _controller.ObtenerMapa(filtro, default);

            //Assert
            Assert.Same(filtro, filtroRecibido);
        }

        [Fact]
        public async Task QueSeDevuelvaBadRequestSiElFiltroDelMapaEsInvalido()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.ObtenerPines(It.IsAny<PublicacionFiltroDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Datos inválidos"));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                (await _controller.ObtenerMapa(new PublicacionFiltroDto(), default)).Result);

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(400, respuesta.StatusCode);
            Assert.Equal(400, problema.Status);
            Assert.Equal("Datos inválidos", problema.Detail);
        }

        #endregion

        #region Resumen y Detalle

        [Fact]
        public async Task QueSeDevuelvaOkConElResumenDeLaPublicacion()
        {
            //Arrange
            var resumenEsperado = new PublicacionResumenDto { Id = 5, Titulo = "Recuerdo" };
            _publicacionServicioMock
                .Setup(s => s.ObtenerResumenPorId(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(resumenEsperado);

            //Act
            var result = await _controller.ObtenerResumen(5, default);

            //Assert
            var respuesta = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(resumenEsperado, respuesta.Value);
            _publicacionServicioMock.Verify(s => s.ObtenerResumenPorId(5, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueSeDevuelvaBadRequestSiElIdDelResumenEsInvalido()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.ObtenerResumenPorId(0, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Datos inválidos"));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                (await _controller.ObtenerResumen(0, default)).Result);

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(400, respuesta.StatusCode);
            Assert.Equal(400, problema.Status);
            Assert.Equal("Datos inválidos", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaNotFoundSiElResumenNoEstaDisponible()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.ObtenerResumenPorId(99, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("La publicación no está disponible."));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                (await _controller.ObtenerResumen(99, default)).Result);

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(404, respuesta.StatusCode);
            Assert.Equal(404, problema.Status);
            Assert.Equal("La publicación no está disponible.", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaOkConElDetalleDeLaPublicacion()
        {
            //Arrange
            var detalleEsperado = new PublicacionDetalleDto { Id = 5, Titulo = "Recuerdo" };
            _publicacionServicioMock
                .Setup(s => s.ObtenerDetallePorId(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(detalleEsperado);

            //Act
            var result = await _controller.ObtenerDetalle(5, default);

            //Assert
            var respuesta = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(detalleEsperado, respuesta.Value);
            _publicacionServicioMock.Verify(s => s.ObtenerDetallePorId(5, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueSeDevuelvaBadRequestSiElIdDelDetalleEsInvalido()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.ObtenerDetallePorId(0, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Datos inválidos"));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                (await _controller.ObtenerDetalle(0, default)).Result);

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(400, respuesta.StatusCode);
            Assert.Equal(400, problema.Status);
            Assert.Equal("Datos inválidos", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaNotFoundSiElDetalleNoEstaDisponible()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.ObtenerDetallePorId(99, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("La publicación no está disponible."));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                (await _controller.ObtenerDetalle(99, default)).Result);

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(404, respuesta.StatusCode);
            Assert.Equal(404, problema.Status);
            Assert.Equal("La publicación no está disponible.", problema.Detail);
        }

        #endregion

        #region Etiquetado de Usuarios

        [Fact]
        public async Task QueSeDevuelvaCreatedSiSeAgreganEtiquetasNuevas()
        {
            //Arrange
            _etiquetadoServicioMock
                .Setup(e => e.EtiquetarUsuarios(5, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);
            var request = new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10, 20 } };

            //Act
            var result = await _controller.EtiquetarUsuarios(5, request, default);

            //Assert
            var respuesta = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, respuesta.StatusCode);
            Assert.Equal(2, JsonSerializer.SerializeToElement(respuesta.Value).GetProperty("cantidadEtiquetasNuevas").GetInt32());
        }

        [Fact]
        public async Task QueSeDevuelvaOkSiLosUsuariosYaEstabanEtiquetados()
        {
            //Arrange
            _etiquetadoServicioMock
                .Setup(e => e.EtiquetarUsuarios(5, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
            var request = new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10, 20 } };

            //Act
            var result = await _controller.EtiquetarUsuarios(5, request, default);

            //Assert
            var respuesta = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, respuesta.StatusCode);
            Assert.Equal(0, JsonSerializer.SerializeToElement(respuesta.Value).GetProperty("cantidadEtiquetasNuevas").GetInt32());
        }

        [Fact]
        public async Task QueSeEnvienAlServicioLaPublicacionYLosUsuariosAEtiquetar()
        {
            //Arrange
            var request = new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10, 20 } };
            int publicacionRecibida = 0;
            IReadOnlyCollection<int>? usuariosRecibidos = null;
            _etiquetadoServicioMock
                .Setup(e => e.EtiquetarUsuarios(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .Callback<int, IReadOnlyCollection<int>, CancellationToken>((p, u, _) =>
                {
                    publicacionRecibida = p;
                    usuariosRecibidos = u;
                })
                .ReturnsAsync(2);

            //Act
            await _controller.EtiquetarUsuarios(5, request, default);

            //Assert
            Assert.Equal(5, publicacionRecibida);
            Assert.Equal(new[] { 10, 20 }, usuariosRecibidos);
        }

        [Fact]
        public async Task QueSeDevuelvaBadRequestSiLosDatosDeEtiquetadoSonInvalidos()
        {
            //Arrange
            _etiquetadoServicioMock
                .Setup(e => e.EtiquetarUsuarios(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Datos inválidos"));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                await _controller.EtiquetarUsuarios(5, new EtiquetarUsuariosRequest(), default));

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(400, respuesta.StatusCode);
            Assert.Equal(400, problema.Status);
            Assert.Equal("Datos inválidos", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaNotFoundSiLaPublicacionOElUsuarioAEtiquetarNoExiste()
        {
            //Arrange
            _etiquetadoServicioMock
                .Setup(e => e.EtiquetarUsuarios(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("El usuario no existe."));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                await _controller.EtiquetarUsuarios(5, new EtiquetarUsuariosRequest(), default));

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(404, respuesta.StatusCode);
            Assert.Equal(404, problema.Status);
            Assert.Equal("El usuario no existe.", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaErrorInternoSinExponerDetallesSiFallaElEtiquetado()
        {
            //Arrange
            _etiquetadoServicioMock
                .Setup(e => e.EtiquetarUsuarios(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("credenciales internas"));

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () =>
                await _controller.EtiquetarUsuarios(5, new EtiquetarUsuariosRequest(), default));

            //Assert
            var respuesta = Assert.IsType<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(500, respuesta.StatusCode);
            Assert.Equal(500, problema.Status);
            Assert.DoesNotContain("credenciales internas", JsonSerializer.Serialize(problema));
            Assert.False(string.IsNullOrWhiteSpace(problema.Detail));
        }

        [Fact]
        public async Task QueSePropagueLaCancelacionDelEtiquetado()
        {
            //Arrange
            using var cancelacion = new CancellationTokenSource();
            cancelacion.Cancel();
            _etiquetadoServicioMock
                .Setup(e => e.EtiquetarUsuarios(5, It.IsAny<IReadOnlyCollection<int>>(), cancelacion.Token))
                .ThrowsAsync(new OperationCanceledException(cancelacion.Token));

            //Act
            var result = await Assert.ThrowsAsync<OperationCanceledException>(
                () => _controller.EtiquetarUsuarios(5, new EtiquetarUsuariosRequest(), cancelacion.Token));

            //Assert
            Assert.Equal(cancelacion.Token, result.CancellationToken);
        }

        #endregion

        #region Reposts y Citas

        [Fact]
        public async Task QueSePuedaRepostearUnaPublicacion()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.Repostear(42, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RepostPublicacionDto
                {
                    PublicacionId = 42,
                    DioRepost = true,
                    CantidadReposts = 1
                });

            //Act
            var result = await _controller.Repostear(42, 10);

            //Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<RepostPublicacionDto>(ok.Value);
            Assert.Equal(42, dto.PublicacionId);
            Assert.True(dto.DioRepost);
            _publicacionServicioMock.Verify(s => s.Repostear(42, 10, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueSePuedaQuitarRepostDeUnaPublicacion()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.QuitarRepost(42, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RepostPublicacionDto
                {
                    PublicacionId = 42,
                    DioRepost = false,
                    CantidadReposts = 0
                });

            //Act
            var result = await _controller.QuitarRepost(42, 10);

            //Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<RepostPublicacionDto>(ok.Value);
            Assert.Equal(42, dto.PublicacionId);
            Assert.False(dto.DioRepost);
            _publicacionServicioMock.Verify(s => s.QuitarRepost(42, 10, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueSePuedaGuardarUnaPublicacion()
        {
            var result = await _controller.Guardar(42, 10);
            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<GuardadoPublicacionDto>(ok.Value);
            Assert.Equal(42, dto.PublicacionId);
            Assert.True(dto.Guardada);
            Assert.Equal(42, _servicio.IdConsultado);
        }

        [Fact]
        public async Task QueSePuedaQuitarGuardadoDeUnaPublicacion()
        {
            var result = await _controller.QuitarGuardado(42, 10);
            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<GuardadoPublicacionDto>(ok.Value);
            Assert.Equal(42, dto.PublicacionId);
            Assert.False(dto.Guardada);
            Assert.Equal(42, _servicio.IdConsultado);
        }

        [Fact]
        public async Task Guardar_DevuelveUnauthorizedSiNoHayUsuario()
        {
            var result = await _controller.Guardar(42, null);
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task QuitarGuardado_DevuelveUnauthorizedSiNoHayUsuario()
        {
            var result = await _controller.QuitarGuardado(42, null);
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task QueSePuedanObtenerPublicacionesGuardadasPorUsuario()
        {
            var result = await _controller.ObtenerGuardadasPorUsuario(10, CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var lista = Assert.IsType<List<PublicacionDetalleDto>>(ok.Value);
            Assert.Equal(10, _servicio.IdConsultado);
            Assert.Single(lista);
        }

        [Fact]
        public async Task QueSePuedaCitarUnaPublicacion()
        {
            //Arrange
            _publicacionServicioMock
                .Setup(s => s.Citar(42, 10, "Excelente foto", It.IsAny<CancellationToken>()))
                .ReturnsAsync(99);
            var request = new CitarPublicacionRequest { AutorId = 10, Descripcion = "Excelente foto" };

            //Act
            var result = await _controller.Citar(42, request);

            //Assert
            var created = Assert.IsType<CreatedResult>(result);
            Assert.Equal("/api/publicaciones/99", created.Location);
            Assert.Equal(99, JsonSerializer.SerializeToElement(created.Value).GetProperty("id").GetInt32());
            _publicacionServicioMock.Verify(s => s.Citar(42, 10, "Excelente foto", It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Manejo de Excepciones y Cancelación

        [Theory]
        [InlineData("crear")]
        [InlineData("mapa")]
        [InlineData("resumen")]
        [InlineData("detalle")]
        public async Task QueSeDevuelvaErrorInternoSinExponerDetallesSiFallaUnaOperacion(string operacion)
        {
            //Arrange
            var error = new InvalidOperationException("credenciales internas");
            _publicacionServicioMock
                .Setup(s => s.Crear(It.IsAny<Publicacion>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<NetTopologySuite.Geometries.Point?>()))
                .ThrowsAsync(error);
            _publicacionServicioMock
                .Setup(s => s.ObtenerPines(It.IsAny<PublicacionFiltroDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(error);
            _publicacionServicioMock
                .Setup(s => s.ObtenerResumenPorId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(error);
            _publicacionServicioMock
                .Setup(s => s.ObtenerDetallePorId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(error);

            //Act
            var result = await MiddlewareTestHelper.Ejecutar(_controller.HttpContext, async () => operacion switch
            {
                "crear" => await _controller.Crear(new CrearPublicacionRequest()),
                "mapa" => (await _controller.ObtenerMapa(new PublicacionFiltroDto(), default)).Result,
                "resumen" => (await _controller.ObtenerResumen(5, default)).Result,
                "detalle" => (await _controller.ObtenerDetalle(5, default)).Result,
                _ => throw new ArgumentOutOfRangeException(nameof(operacion))
            });

            //Assert
            var respuesta = Assert.IsType<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(500, respuesta.StatusCode);
            Assert.Equal(500, problema.Status);
            Assert.False(string.IsNullOrWhiteSpace(problema.Detail));
            Assert.DoesNotContain("credenciales internas", JsonSerializer.Serialize(problema));
        }

        [Theory]
        [InlineData("crear")]
        [InlineData("mapa")]
        [InlineData("resumen")]
        [InlineData("detalle")]
        public async Task QueSePropagueLaCancelacionDeLaSolicitud(string operacion)
        {
            //Arrange
            using var cancelacion = new CancellationTokenSource();
            cancelacion.Cancel();
            _controller.HttpContext.RequestAborted = cancelacion.Token;

            var error = new OperationCanceledException(cancelacion.Token);
            _publicacionServicioMock
                .Setup(s => s.Crear(It.IsAny<Publicacion>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<NetTopologySuite.Geometries.Point?>()))
                .ThrowsAsync(error);
            _publicacionServicioMock
                .Setup(s => s.ObtenerPines(It.IsAny<PublicacionFiltroDto>(), cancelacion.Token))
                .ThrowsAsync(error);
            _publicacionServicioMock
                .Setup(s => s.ObtenerResumenPorId(It.IsAny<int>(), cancelacion.Token))
                .ThrowsAsync(error);
            _publicacionServicioMock
                .Setup(s => s.ObtenerDetallePorId(It.IsAny<int>(), cancelacion.Token))
                .ThrowsAsync(error);

            //Act
            var result = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                switch (operacion)
                {
                    case "crear":
                        await _controller.Crear(new CrearPublicacionRequest());
                        break;
                    case "mapa":
                        await _controller.ObtenerMapa(new PublicacionFiltroDto(), cancelacion.Token);
                        break;
                    case "resumen":
                        await _controller.ObtenerResumen(5, cancelacion.Token);
                        break;
                    case "detalle":
                        await _controller.ObtenerDetalle(5, cancelacion.Token);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operacion));
                }
            });

            //Assert
            Assert.Equal(cancelacion.Token, result.CancellationToken);
        }

        #endregion
    }
}
