using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/publicaciones")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public class PublicacionesController : ControllerBase
    {
        private readonly IPublicacionServicio _publicacionServicio;
        private readonly IEtiquetadoPublicacionServicio _etiquetadoPublicacionServicio;
        private readonly ILogger<PublicacionesController> _logger;

        public PublicacionesController(
            IPublicacionServicio publicacionServicio,
            IEtiquetadoPublicacionServicio etiquetadoPublicacionServicio,
            ILogger<PublicacionesController> logger)
        {
            _publicacionServicio = publicacionServicio;
            _etiquetadoPublicacionServicio = etiquetadoPublicacionServicio;
            _logger = logger;
        }

        [HttpGet("mapa")]
        [ProducesResponseType(typeof(ResultadoMapaDto), StatusCodes.Status200OK)]
        public Task<ActionResult<ResultadoMapaDto>> ObtenerMapa(
            [FromQuery] PublicacionFiltroDto filtro,
            CancellationToken cancellationToken)
        {
            return EjecutarConsulta(() => _publicacionServicio.ObtenerPines(
                filtro, cancellationToken), "obtener el mapa", cancellationToken);
        }

        [HttpGet("{id:int}/resumen")]
        [ProducesResponseType(typeof(PublicacionResumenDto), StatusCodes.Status200OK)]
        public Task<ActionResult<PublicacionResumenDto>> ObtenerResumen(
            int id,
            CancellationToken cancellationToken)
        {
            return EjecutarConsulta(() => _publicacionServicio.ObtenerResumenPorId(
                id, cancellationToken), "obtener el resumen de la publicación", cancellationToken);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(PublicacionDetalleDto), StatusCodes.Status200OK)]
        public Task<ActionResult<PublicacionDetalleDto>> ObtenerDetalle(
            int id,
            CancellationToken cancellationToken)
        {
            return EjecutarConsulta(() => _publicacionServicio.ObtenerDetallePorId(
                id, cancellationToken), "obtener el detalle de la publicación", cancellationToken);
        }

        [HttpGet("{id:int}/comentarios")]
        [ProducesResponseType(typeof(ResultadoPaginadoDto<ComentarioDto>), StatusCodes.Status200OK)]
        public Task<ActionResult<ResultadoPaginadoDto<ComentarioDto>>> ObtenerComentarios(
            int id,
            [FromQuery] string? cursor = null,
            [FromQuery] int limite = 20,
            CancellationToken cancellationToken = default)
        {
            return EjecutarConsulta(() => _publicacionServicio.ObtenerComentarios(
                id, cursor, limite, cancellationToken), "obtener los comentarios", cancellationToken);
        }

        private async Task<ActionResult<T>> EjecutarConsulta<T>(
            Func<Task<T>> consulta, string operacion, CancellationToken cancellationToken)
        {
            var resultado = await EjecutarOperacion(
                async () => Ok(await consulta()), operacion, cancellationToken);
            return (ActionResult)resultado;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public Task<IActionResult> Crear([FromForm] CrearPublicacionRequest request)
        {
            return EjecutarOperacion(async () =>
            {
                using var stream = request.Archivo?.OpenReadStream();

                var dto = new CrearPublicacionDto
                {
                    Titulo = request.Titulo,
                    Descripcion = request.Descripcion,
                    Fecha = request.Fecha,
                    Categoria = request.Categoria,
                    NombreLugar = request.NombreLugar,
                    Latitud = request.Latitud,
                    Longitud = request.Longitud,
                    TipoMultimedia = request.TipoMultimedia,
                    Archivo = stream,
                    NombreArchivo = request.Archivo?.FileName,
                    UrlArchivo = request.UrlArchivo,
                    AutorId = request.AutorId
                };

                var id = await _publicacionServicio.Crear(dto);

                return Created(
                    $"/api/publicaciones/{id}",
                    new { id });
            }, "crear la publicación", HttpContext.RequestAborted);
        }

        [HttpPost("compartir")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public Task<IActionResult> Compartir(
            [FromBody] CompartirPublicacionRequest request,
            CancellationToken cancellationToken = default)
        {
            return EjecutarOperacion(async () =>
            {
                var dto = new CompartirPublicacionDto
                {
                    PublicacionOriginalId = request.PublicacionOriginalId,
                    AutorId = request.AutorId,
                    Descripcion = request.Descripcion ?? string.Empty
                };

                var id = await _publicacionServicio.Compartir(dto, cancellationToken);

                return Created(
                    $"/api/publicaciones/{id}",
                    new { id });
            }, "compartir la publicación", cancellationToken);
        }

        [HttpPost("{publicacionId:int}/etiquetas")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public Task<IActionResult> EtiquetarUsuarios(
            int publicacionId, [FromBody] EtiquetarUsuariosRequest request, CancellationToken cancellationToken)
        {
            return EjecutarOperacion(async () =>
            {
                var cantidadEtiquetasNuevas = await _etiquetadoPublicacionServicio.EtiquetarUsuarios(
                    publicacionId, request.UsuariosIds, cancellationToken);

                var respuesta = new
                {
                    mensaje = cantidadEtiquetasNuevas > 0
                        ? "Se etiquetaron los usuarios en la publicacion."
                        : "usuarios ya etiquetados en la publicacion",
                    publicacionId,
                    cantidadEtiquetasNuevas
                };

                return cantidadEtiquetasNuevas > 0
                    ? StatusCode(StatusCodes.Status201Created, respuesta)
                    : Ok(respuesta);
            }, "etiquetar usuarios en la publicación", cancellationToken);
        }

        // Todas las acciones comparten el mismo contrato de errores.
        private async Task<IActionResult> EjecutarOperacion(
            Func<Task<IActionResult>> accion, string operacion, CancellationToken cancellationToken)
        {
            try
            {
                return await accion();
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested || HttpContext.RequestAborted.IsCancellationRequested)
            {
                // Una solicitud cancelada no debe convertirse en un error interno.
                throw;
            }
            catch (ArgumentException exception)
            {
                return CrearProblema(400, "Solicitud inválida", exception.Message);
            }
            catch (KeyNotFoundException exception)
            {
                return CrearProblema(404, "Recurso no encontrado", exception.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error al {Operacion}. TraceId: {TraceId}",
                    operacion, HttpContext.TraceIdentifier);

                return CrearProblema(500, "Error interno del servidor",
                    $"No se pudo {operacion}. Intentá nuevamente más tarde.");
            }
        }

        private ObjectResult CrearProblema(int estado, string titulo, string detalle)
        {
            var problema = new ProblemDetails
            {
                Status = estado,
                Title = titulo,
                Detail = detalle,
                Instance = HttpContext.Request.Path
            };
            problema.Extensions["traceId"] = HttpContext.TraceIdentifier;

            var respuesta = new ObjectResult(problema) { StatusCode = estado };
            respuesta.ContentTypes.Add("application/problem+json");
            return respuesta;
        }
    }
}

