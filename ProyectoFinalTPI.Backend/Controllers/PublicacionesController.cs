using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;

using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Entidades;

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
        private readonly ApplicationDbContext _context;

        public PublicacionesController(
            IPublicacionServicio publicacionServicio,
            IEtiquetadoPublicacionServicio etiquetadoPublicacionServicio,
            ILogger<PublicacionesController> logger,
            ApplicationDbContext context = null!)
        {
            _publicacionServicio = publicacionServicio;
            _etiquetadoPublicacionServicio = etiquetadoPublicacionServicio;
            _logger = logger;
            _context = context;
        }

        [HttpGet("test-users")]
        public IActionResult GetTestUsers()
        {
            return Ok(_context.Usuarios.Select(u => new { u.Id, u.Username }).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var publicaciones = await _context.Publicaciones
                .Include(p => p.Lugar)
                .Include(p => p.Usuario)
                .Include(p => p.PublicacionOriginal)
                    .ThenInclude(po => po.Usuario)
                .Include(p => p.PublicacionOriginal)
                    .ThenInclude(po => po.Lugar)
                .Include(p => p.PublicacionOriginal)
                    .ThenInclude(po => po.PublicacionOriginal)
                .Where(p => !p.EstaOculto && (p.PublicacionOriginal == null || !p.PublicacionOriginal.EstaOculto))
                .OrderByDescending(p => p.FechaCreación)
                .ToListAsync();

            var result = publicaciones.Select(p => {
                var esRepost = p.PublicacionOriginal != null;
                var original = p.PublicacionOriginal;

                var rootOriginal = original;
                while (rootOriginal?.PublicacionOriginal != null && string.IsNullOrEmpty(rootOriginal.Descripcion))
                {
                    rootOriginal = rootOriginal.PublicacionOriginal;
                }
                var rootId = rootOriginal?.Id ?? original?.Id;
                var rootAuthor = rootOriginal?.Usuario?.Username ?? original?.Usuario?.Username ?? "Desconocido";

                var year = esRepost && rootOriginal != null
                    ? (rootOriginal.Fecha.Year <= 2015 ? rootOriginal.Fecha.Year : rootOriginal.Fecha.Year)
                    : p.Fecha.Year;
                var place = p.Lugar != null ? p.Lugar.Nombre : (rootOriginal?.Lugar?.Nombre ?? "");
                var lat = p.Lugar != null ? p.Lugar.Coordenadas.Y : (rootOriginal?.Lugar?.Coordenadas.Y ?? 0);
                var lng = p.Lugar != null ? p.Lugar.Coordenadas.X : (rootOriginal?.Lugar?.Coordenadas.X ?? 0);
                var description = esRepost && rootOriginal != null
                    ? (string.IsNullOrEmpty(rootOriginal.Descripcion) ? p.Descripcion : rootOriginal.Descripcion)
                    : p.Descripcion;
                var urlMedia = !string.IsNullOrEmpty(p.UrlMultimedia)
                    ? p.UrlMultimedia
                    : (rootOriginal?.UrlMultimedia ?? string.Empty);
                var tipoMedia = !string.IsNullOrEmpty(p.UrlMultimedia)
                    ? p.TipoMultimedia
                    : (rootOriginal?.TipoMultimedia ?? p.TipoMultimedia);

                return new {
                    id = p.Id.ToString(),
                    title = rootOriginal?.Titulo ?? p.Titulo,
                    year = year,
                    author = p.Usuario?.Username ?? "Desconocido",
                    place = place,
                    lat = lat,
                    lng = lng,
                    description = description,
                    category = p.Categoria.ToString().Replace("Recuerdos_Personales", "Personales"),
                    source = "db",
                    image = tipoMedia == MultimediaEnum.Foto ? urlMedia : null,
                    media = (!string.IsNullOrEmpty(urlMedia) && tipoMedia != MultimediaEnum.Foto) ? new {
                        url = urlMedia,
                        kind = tipoMedia == MultimediaEnum.Video ? "video" : "image"
                    } : null,
                    repost = esRepost && original != null ? new {
                        id = p.Id.ToString(),
                        originalId = rootId?.ToString() ?? original.Id.ToString(),
                        author = rootAuthor,
                        comment = !string.IsNullOrEmpty(p.Descripcion) ? p.Descripcion : null
                    } : null
                };
            });

            return Ok(result);
        }

        private int? ObtenerUsuarioIdAutenticado()
        {
            try
            {
                var idString = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User?.FindFirst("sub")?.Value;

                return int.TryParse(idString, out var id) ? id : null;
            }
            catch
            {
                return null;
            }
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
            var autorIdAutenticado = ObtenerUsuarioIdAutenticado();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : request.AutorId;

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
                    AutorId = autorIdFinal
                };

                var id = await _publicacionServicio.Crear(dto);

                return Created(
                    $"/api/publicaciones/{id}",
                    new { id });
            }, "crear la publicación", HttpContext?.RequestAborted ?? default);
        }

        [HttpPost("compartir")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public Task<IActionResult> Compartir(
            [FromBody] CompartirPublicacionRequest request,
            CancellationToken cancellationToken = default)
        {
            var autorIdAutenticado = ObtenerUsuarioIdAutenticado();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : request.AutorId;

            return EjecutarOperacion(async () =>
            {
                var dto = new CompartirPublicacionDto
                {
                    PublicacionOriginalId = request.PublicacionOriginalId,
                    AutorId = autorIdFinal,
                    Descripcion = request.Descripcion ?? string.Empty
                };

                var id = await _publicacionServicio.Compartir(dto, cancellationToken);

                return Created(
                    $"/api/publicaciones/{id}",
                    new { id });
            }, "compartir la publicación", cancellationToken);
        }

        [HttpDelete("compartir/{originalId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> EliminarRepost(
            int originalId,
            [FromQuery] int? autorId = null,
            CancellationToken cancellationToken = default)
        {
            var autorIdAutenticado = ObtenerUsuarioIdAutenticado();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : autorId;

            return EjecutarOperacion(async () =>
            {
                await _publicacionServicio.EliminarRepost(originalId, autorIdFinal, cancellationToken);
                return Ok(new { mensaje = "Repost eliminado con éxito.", id = originalId });
            }, "eliminar el repost", cancellationToken);
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> Eliminar(
            int id,
            [FromQuery] int? autorId = null,
            CancellationToken cancellationToken = default)
        {
            var autorIdAutenticado = ObtenerUsuarioIdAutenticado();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : autorId;

            return EjecutarOperacion(async () =>
            {
                await _publicacionServicio.Eliminar(id, autorIdFinal, cancellationToken);
                return Ok(new { mensaje = "Publicación eliminada con éxito.", id });
            }, "eliminar la publicación", cancellationToken);
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
                cancellationToken.IsCancellationRequested || (HttpContext?.RequestAborted.IsCancellationRequested ?? false))
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
                    operacion, HttpContext?.TraceIdentifier ?? "N/A");

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
                Instance = HttpContext?.Request.Path
            };
            if (HttpContext != null)
            {
                problema.Extensions["traceId"] = HttpContext.TraceIdentifier;
            }

            var respuesta = new ObjectResult(problema) { StatusCode = estado };
            respuesta.ContentTypes.Add("application/problem+json");
            return respuesta;
        }
    }
}

