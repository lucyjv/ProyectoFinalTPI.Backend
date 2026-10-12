using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
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
        private readonly PublicacionServicioFake _servicio = new();
        private readonly EtiquetadoServicioFake _etiquetado = new();
        private readonly PublicacionesController _controller;

        public PublicacionesControllerTests()
        {
            _controller = new PublicacionesController(
                _servicio, _etiquetado, NullLogger<PublicacionesController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        [Fact]
        public async Task QueSeDevuelvaCreatedConElIdYLaUbicacionDeLaPublicacionCreada()
        {
            //Arrange
            _servicio.IdCreado = 25;
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
            var request = new CrearPublicacionRequest
            {
                Titulo = "Recuerdo", Descripcion = "Una visita", AutorId = 10,
                NombreLugar = "Obelisco", Latitud = -34.6, Longitud = -58.3,
                Categoria = CategoriaEnum.Lugares, TipoMultimedia = MultimediaEnum.Foto,
                Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UrlArchivo = "https://example.com/foto.jpg"
            };

            //Act
            await _controller.Crear(request);

            //Assert
            Assert.NotNull(_servicio.CrearDto);
            Assert.Equal("Recuerdo", _servicio.CrearDto.Titulo);
            Assert.Equal("Obelisco", _servicio.CrearDto.NombreLugar);
            Assert.Equal(10, _servicio.CrearDto.AutorId);
            Assert.Equal(request.UrlArchivo, _servicio.CrearDto.UrlArchivo);
            Assert.Null(_servicio.CrearDto.Archivo);
        }

        [Fact]
        public async Task QueSeEnvienAlServicioElArchivoYSuNombre()
        {
            //Arrange
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var request = new CrearPublicacionRequest
            {
                Archivo = new FormFile(stream, 0, stream.Length, "Archivo", "foto.jpg")
            };

            //Act
            await _controller.Crear(request);

            //Assert
            Assert.Equal("foto.jpg", _servicio.CrearDto!.NombreArchivo);
            Assert.Equal(new byte[] { 1, 2, 3 }, _servicio.ArchivoRecibido);
        }

        [Fact]
        public async Task QueSeDevuelvaNotFoundAlCrearSiElAutorNoExiste()
        {
            //Arrange
            _servicio.Error = new KeyNotFoundException("El usuario indicado no existe.");

            //Act
            var result = await _controller.Crear(new CrearPublicacionRequest());

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(404, respuesta.StatusCode);
            Assert.Equal(404, problema.Status);
            Assert.Equal("El usuario indicado no existe.", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaCreatedAlCompartirUnaPublicacion()
        {
            //Arrange
            _servicio.IdCreado = 30;

            //Act
            var result = await _controller.Compartir(new CompartirPublicacionRequest());

            //Assert
            var respuesta = Assert.IsType<CreatedResult>(result);
            Assert.Equal(201, respuesta.StatusCode);
            Assert.Equal("/api/publicaciones/30", respuesta.Location);
            Assert.Equal(30, JsonSerializer.SerializeToElement(respuesta.Value).GetProperty("id").GetInt32());
        }

        [Fact]
        public async Task QueSeEnvienAlServicioLosDatosParaCompartir()
        {
            //Arrange
            var request = new CompartirPublicacionRequest
            {
                PublicacionOriginalId = 5, AutorId = 10, Descripcion = "Compartido"
            };

            //Act
            await _controller.Compartir(request);

            //Assert
            Assert.NotNull(_servicio.CompartirDto);
            Assert.Equal(5, _servicio.CompartirDto.PublicacionOriginalId);
            Assert.Equal(10, _servicio.CompartirDto.AutorId);
            Assert.Equal("Compartido", _servicio.CompartirDto.Descripcion);
        }

        [Fact]
        public async Task QueSeEnvieUnaDescripcionVaciaAlCompartirSiEsNula()
        {
            //Arrange
            var request = new CompartirPublicacionRequest { Descripcion = null! };

            //Act
            await _controller.Compartir(request);

            //Assert
            Assert.Equal(string.Empty, _servicio.CompartirDto!.Descripcion);
        }

        [Fact]
        public async Task QueSeDevuelvaNotFoundSiNoSePuedeCompartirLaPublicacion()
        {
            //Arrange
            _servicio.Error = new KeyNotFoundException("La publicación no está disponible.");

            //Act
            var result = await _controller.Compartir(new CompartirPublicacionRequest());

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(404, respuesta.StatusCode);
            Assert.Equal(404, problema.Status);
            Assert.Equal("La publicación no está disponible.", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaOkConLosPinesDelMapa()
        {
            //Arrange
            _servicio.Mapa = new ResultadoMapaDto
            {
                Pines = new List<PublicacionPinDto> { new() { Id = 5 } }, HayMas = true
            };

            //Act
            var result = await _controller.ObtenerMapa(new PublicacionFiltroDto(), default);

            //Assert
            var respuesta = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(_servicio.Mapa, respuesta.Value);
        }

        [Fact]
        public async Task QueSeEnvieAlServicioElFiltroDelMapa()
        {
            //Arrange
            var filtro = new PublicacionFiltroDto
            {
                MinLatitud = -35, MaxLatitud = -34, MinLongitud = -59, MaxLongitud = -58,
                Categoria = CategoriaEnum.Lugares
            };

            //Act
            await _controller.ObtenerMapa(filtro, default);

            //Assert
            Assert.Same(filtro, _servicio.Filtro);
        }

        [Fact]
        public async Task QueSeDevuelvaBadRequestSiElFiltroDelMapaEsInvalido()
        {
            //Arrange
            _servicio.Error = new ArgumentException("Datos inválidos");

            //Act
            var result = (await _controller.ObtenerMapa(new PublicacionFiltroDto(), default)).Result;

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(400, respuesta.StatusCode);
            Assert.Equal(400, problema.Status);
            Assert.Equal("Datos inválidos", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaOkConElResumenDeLaPublicacion()
        {
            //Arrange
            _servicio.Resumen = new PublicacionResumenDto { Id = 5, Titulo = "Recuerdo" };

            //Act
            var result = await _controller.ObtenerResumen(5, default);

            //Assert
            var respuesta = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(_servicio.Resumen, respuesta.Value);
            Assert.Equal(5, _servicio.IdConsultado);
        }

        [Fact]
        public async Task QueSeDevuelvaBadRequestSiElIdDelResumenEsInvalido()
        {
            //Arrange
            _servicio.Error = new ArgumentException("Datos inválidos");

            //Act
            var result = (await _controller.ObtenerResumen(0, default)).Result;

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
            _servicio.Error = new KeyNotFoundException("La publicación no está disponible.");

            //Act
            var result = (await _controller.ObtenerResumen(99, default)).Result;

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
            _servicio.Detalle = new PublicacionDetalleDto { Id = 5, Titulo = "Recuerdo" };

            //Act
            var result = await _controller.ObtenerDetalle(5, default);

            //Assert
            var respuesta = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(_servicio.Detalle, respuesta.Value);
            Assert.Equal(5, _servicio.IdConsultado);
        }

        [Fact]
        public async Task QueSeDevuelvaBadRequestSiElIdDelDetalleEsInvalido()
        {
            //Arrange
            _servicio.Error = new ArgumentException("Datos inválidos");

            //Act
            var result = (await _controller.ObtenerDetalle(0, default)).Result;

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
            _servicio.Error = new KeyNotFoundException("La publicación no está disponible.");

            //Act
            var result = (await _controller.ObtenerDetalle(99, default)).Result;

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(404, respuesta.StatusCode);
            Assert.Equal(404, problema.Status);
            Assert.Equal("La publicación no está disponible.", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaOkConLaPaginaDeComentarios()
        {
            //Arrange
            _servicio.Comentarios = new ResultadoPaginadoDto<ComentarioDto>
            {
                Items = new List<ComentarioDto> { new() { Id = 7 } }, SiguienteCursor = "cursor"
            };

            //Act
            var result = await _controller.ObtenerComentarios(5);

            //Assert
            var respuesta = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(_servicio.Comentarios, respuesta.Value);
        }

        [Fact]
        public async Task QueSeEnvienAlServicioElIdElCursorYElLimiteDeComentarios()
        {
            //Arrange
            var cursor = "cursor-de-prueba";

            //Act
            await _controller.ObtenerComentarios(5, cursor, 10);

            //Assert
            Assert.Equal(5, _servicio.IdConsultado);
            Assert.Equal(cursor, _servicio.Cursor);
            Assert.Equal(10, _servicio.Limite);
        }

        [Fact]
        public async Task QueSeUtiliceElLimitePredeterminadoDeVeinteComentarios()
        {
            //Arrange
            var id = 5;

            //Act
            await _controller.ObtenerComentarios(id);

            //Assert
            Assert.Equal(20, _servicio.Limite);
            Assert.Null(_servicio.Cursor);
        }

        [Fact]
        public async Task QueSeDevuelvaBadRequestSiLosParametrosDeComentariosSonInvalidos()
        {
            //Arrange
            _servicio.Error = new ArgumentException("Datos inválidos");

            //Act
            var result = (await _controller.ObtenerComentarios(5, "inválido", 20)).Result;

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(400, respuesta.StatusCode);
            Assert.Equal(400, problema.Status);
            Assert.Equal("Datos inválidos", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaNotFoundSiLaPublicacionDeLosComentariosNoEstaDisponible()
        {
            //Arrange
            _servicio.Error = new KeyNotFoundException("La publicación no está disponible.");

            //Act
            var result = (await _controller.ObtenerComentarios(99)).Result;

            //Assert
            var respuesta = Assert.IsAssignableFrom<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(404, respuesta.StatusCode);
            Assert.Equal(404, problema.Status);
            Assert.Equal("La publicación no está disponible.", problema.Detail);
        }

        [Fact]
        public async Task QueSeDevuelvaCreatedSiSeAgreganEtiquetasNuevas()
        {
            //Arrange
            _etiquetado.Cantidad = 2;
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
            _etiquetado.Cantidad = 0;
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

            //Act
            await _controller.EtiquetarUsuarios(5, request, default);

            //Assert
            Assert.Equal(5, _etiquetado.PublicacionId);
            Assert.Equal(new[] { 10, 20 }, _etiquetado.UsuariosIds);
        }

        [Fact]
        public async Task QueSeDevuelvaBadRequestSiLosDatosDeEtiquetadoSonInvalidos()
        {
            //Arrange
            _etiquetado.Error = new ArgumentException("Datos inválidos");

            //Act
            var result = await _controller.EtiquetarUsuarios(5, new EtiquetarUsuariosRequest(), default);

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
            _etiquetado.Error = new KeyNotFoundException("El usuario no existe.");

            //Act
            var result = await _controller.EtiquetarUsuarios(5, new EtiquetarUsuariosRequest(), default);

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
            _etiquetado.Error = new InvalidOperationException("credenciales internas");

            //Act
            var result = await _controller.EtiquetarUsuarios(5, new EtiquetarUsuariosRequest(), default);

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
            _etiquetado.Error = new OperationCanceledException(cancelacion.Token);

            //Act
            var result = await Assert.ThrowsAsync<OperationCanceledException>(
                () => _controller.EtiquetarUsuarios(5, new EtiquetarUsuariosRequest(), cancelacion.Token));

            //Assert
            Assert.Equal(cancelacion.Token, result.CancellationToken);
        }

        [Theory]
        [InlineData("crear")]
        [InlineData("compartir")]
        [InlineData("mapa")]
        [InlineData("resumen")]
        [InlineData("detalle")]
        [InlineData("comentarios")]
        public async Task QueSeDevuelvaErrorInternoSinExponerDetallesSiFallaUnaOperacion(string operacion)
        {
            //Arrange
            _servicio.Error = new InvalidOperationException("credenciales internas");

            //Act
            var result = operacion switch
            {
                "crear" => await _controller.Crear(new CrearPublicacionRequest()),
                "compartir" => await _controller.Compartir(new CompartirPublicacionRequest()),
                "mapa" => (await _controller.ObtenerMapa(new PublicacionFiltroDto(), default)).Result,
                "resumen" => (await _controller.ObtenerResumen(5, default)).Result,
                "detalle" => (await _controller.ObtenerDetalle(5, default)).Result,
                _ => (await _controller.ObtenerComentarios(5)).Result
            };

            //Assert
            var respuesta = Assert.IsType<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(500, respuesta.StatusCode);
            Assert.Equal(500, problema.Status);
            Assert.False(string.IsNullOrWhiteSpace(problema.Detail));
            Assert.DoesNotContain("credenciales internas", JsonSerializer.Serialize(problema));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task QueSeDevuelvaBadRequestSiElServicioRechazaLaCreacionOElCompartido(bool compartir)
        {
            //Arrange
            _servicio.Error = new ArgumentException("Datos inválidos");

            //Act
            var result = compartir
                ? await _controller.Compartir(new CompartirPublicacionRequest())
                : await _controller.Crear(new CrearPublicacionRequest());

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
            var result = await _controller.Crear(request);

            //Assert
            var respuesta = Assert.IsType<ObjectResult>(result);
            var problema = Assert.IsType<ProblemDetails>(respuesta.Value);
            Assert.Equal(500, respuesta.StatusCode);
            Assert.Equal("prueba-archivo", problema.Extensions["traceId"]);
            Assert.Equal("/api/publicaciones", problema.Instance);
            Assert.Contains("application/problem+json", respuesta.ContentTypes);
            Assert.DoesNotContain("ruta interna", JsonSerializer.Serialize(problema));
            Assert.Null(_servicio.CrearDto);
        }

        [Theory]
        [InlineData("crear")]
        [InlineData("compartir")]
        [InlineData("mapa")]
        [InlineData("resumen")]
        [InlineData("detalle")]
        [InlineData("comentarios")]
        public async Task QueSePropagueLaCancelacionDeLaSolicitud(string operacion)
        {
            //Arrange
            using var cancelacion = new CancellationTokenSource();
            cancelacion.Cancel();
            _controller.HttpContext.RequestAborted = cancelacion.Token;
            _servicio.Error = new OperationCanceledException(cancelacion.Token);

            //Act
            var result = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                switch (operacion)
                {
                    case "crear":
                        await _controller.Crear(new CrearPublicacionRequest());
                        break;
                    case "compartir":
                        await _controller.Compartir(new CompartirPublicacionRequest(), cancelacion.Token);
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
                        await _controller.ObtenerComentarios(5, cancellationToken: cancelacion.Token);
                        break;
                }
            });

            //Assert
            Assert.Equal(cancelacion.Token, result.CancellationToken);
        }

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

        private class PublicacionServicioFake : IPublicacionServicio
        {
            public Exception? Error { get; set; }
            public int IdCreado { get; set; } = 1;
            public CrearPublicacionDto? CrearDto { get; private set; }
            public CompartirPublicacionDto? CompartirDto { get; private set; }
            public byte[]? ArchivoRecibido { get; private set; }
            public ResultadoMapaDto Mapa { get; set; } = new();
            public PublicacionFiltroDto? Filtro { get; private set; }
            public PublicacionResumenDto Resumen { get; set; } = new();
            public PublicacionDetalleDto Detalle { get; set; } = new();
            public ResultadoPaginadoDto<ComentarioDto> Comentarios { get; set; } = new();
            public int IdConsultado { get; private set; }
            public string? Cursor { get; private set; }
            public int Limite { get; private set; }

            public async Task<int> Crear(CrearPublicacionDto dto)
            {
                CrearDto = dto;
                if (Error != null) throw Error;
                if (dto.Archivo != null)
                {
                    using var copia = new MemoryStream();
                    await dto.Archivo.CopyToAsync(copia);
                    ArchivoRecibido = copia.ToArray();
                }
                return IdCreado;
            }

            public Task<int> Compartir(CompartirPublicacionDto dto, CancellationToken cancellationToken = default)
            {
                CompartirDto = dto;
                return Error == null ? Task.FromResult(IdCreado) : Task.FromException<int>(Error);
            }

            public Task<ResultadoMapaDto> ObtenerPines(PublicacionFiltroDto filtro, CancellationToken cancellationToken = default)
            {
                Filtro = filtro;
                return Error == null ? Task.FromResult(Mapa) : Task.FromException<ResultadoMapaDto>(Error);
            }

            public Task<PublicacionResumenDto> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default)
            {
                IdConsultado = id;
                return Error == null ? Task.FromResult(Resumen) : Task.FromException<PublicacionResumenDto>(Error);
            }

            public Task<PublicacionDetalleDto> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default)
            {
                IdConsultado = id;
                return Error == null ? Task.FromResult(Detalle) : Task.FromException<PublicacionDetalleDto>(Error);
            }

            public Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerComentarios(int publicacionId, string? cursor, int limite, CancellationToken cancellationToken = default)
            {
                IdConsultado = publicacionId;
                Cursor = cursor;
                Limite = limite;
                return Error == null ? Task.FromResult(Comentarios) : Task.FromException<ResultadoPaginadoDto<ComentarioDto>>(Error);
            }

            public List<Publicacion> ObtenerPublicaciones() => throw new NotImplementedException();
            public List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria) => throw new NotImplementedException();
            public Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerFeedSeguidos(int usuarioId, string? cursor, int limite, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(int usuarioId, string? cursor, int limite, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class EtiquetadoServicioFake : IEtiquetadoPublicacionServicio
        {
            public Exception? Error { get; set; }
            public int Cantidad { get; set; }
            public int PublicacionId { get; private set; }
            public IReadOnlyCollection<int>? UsuariosIds { get; private set; }

            public Task<int> EtiquetarUsuarios(int publicacionId, IReadOnlyCollection<int> usuariosIds, CancellationToken cancellationToken = default)
            {
                PublicacionId = publicacionId;
                UsuariosIds = usuariosIds;
                return Error == null ? Task.FromResult(Cantidad) : Task.FromException<int>(Error);
            }
        }
    }
}

