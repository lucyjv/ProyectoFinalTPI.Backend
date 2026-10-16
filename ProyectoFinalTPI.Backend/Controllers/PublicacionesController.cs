using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;
using ProyectoFinalTPI.Backend.Mappings;

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
        private readonly IMeGustaPublicacionServicio? _meGustaPublicacionServicio;

        public PublicacionesController(
            IPublicacionServicio publicacionServicio,
            IEtiquetadoPublicacionServicio etiquetadoPublicacionServicio,
            ILogger<PublicacionesController>? logger = null,
            ApplicationDbContext context = null!,
            IMeGustaPublicacionServicio? meGustaPublicacionServicio = null)
        {
            _publicacionServicio = publicacionServicio;
            _etiquetadoPublicacionServicio = etiquetadoPublicacionServicio;
            _logger = logger ?? NullLogger<PublicacionesController>.Instance;
            _context = context;
            _meGustaPublicacionServicio = meGustaPublicacionServicio;
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
                .ThenInclude(po => po!.Usuario)
            .Include(p => p.PublicacionOriginal)
                .ThenInclude(po => po!.Lugar)
            .Include(p => p.PublicacionOriginal)
                .ThenInclude(po => po!.PublicacionOriginal)
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
        public async Task<ActionResult<ResultadoMapaDto>> ObtenerMapa(
            [FromQuery] PublicacionFiltroDto filtro,
            CancellationToken cancellationToken)
        {
            return Ok(await _publicacionServicio.ObtenerPines(
                filtro, cancellationToken));
        }

        [HttpGet("{id:int}/resumen")]
        [ProducesResponseType(typeof(PublicacionResumenDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PublicacionResumenDto>> ObtenerResumen(
            int id,
            CancellationToken cancellationToken)
        {
            return Ok(await _publicacionServicio.ObtenerResumenPorId(
                id, cancellationToken));
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(PublicacionDetalleDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PublicacionDetalleDto>> ObtenerDetalle(
            int id,
            CancellationToken cancellationToken)
        {
            var detalle = await _publicacionServicio.ObtenerDetallePorId(id, cancellationToken);
            if (_meGustaPublicacionServicio != null)
            {
                await _meGustaPublicacionServicio.CompletarResumenes(
                    [detalle], ObtenerUsuarioIdAutenticado() ?? 0, cancellationToken);
            }

            return Ok(detalle);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Crear([FromForm] CrearPublicacionRequest request)
        {
            var autorIdAutenticado = ObtenerUsuarioIdAutenticado();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : request.AutorId;

            using var stream = request.Archivo?.OpenReadStream();

            var id = await _publicacionServicio.Crear(
                request.ToCrearPublicacionDto(autorIdFinal, stream));

            return Created(
                $"/api/publicaciones/{id}",
                new { id });
        }

        [HttpPost("compartir")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Compartir(
            [FromBody] CompartirPublicacionRequest request,
            CancellationToken cancellationToken = default)
        {
            var autorIdAutenticado = ObtenerUsuarioIdAutenticado();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : request.AutorId;

            var id = await _publicacionServicio.Compartir(
                request.ToCompartirPublicacionDto(autorIdFinal), cancellationToken);

            return Created(
                $"/api/publicaciones/{id}",
                new { id });
        }

        [HttpDelete("compartir/{originalId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EliminarRepost(
            int originalId,
            [FromQuery] int? autorId = null,
            CancellationToken cancellationToken = default)
        {
            var autorIdAutenticado = ObtenerUsuarioIdAutenticado();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : autorId;

            await _publicacionServicio.EliminarRepost(originalId, autorIdFinal, cancellationToken);
            return Ok(new { mensaje = "Repost eliminado con éxito.", id = originalId });
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Eliminar(
            int id,
            [FromQuery] int? autorId = null,
            CancellationToken cancellationToken = default)
        {
            var autorIdAutenticado = ObtenerUsuarioIdAutenticado();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : autorId;

            await _publicacionServicio.Eliminar(id, autorIdFinal, cancellationToken);
            return Ok(new { mensaje = "Publicación eliminada con éxito.", id });
        }

        [HttpPost("{publicacionId:int}/etiquetas")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> EtiquetarUsuarios(
            int publicacionId, [FromBody] EtiquetarUsuariosRequest request, CancellationToken cancellationToken)
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
        }

        private async Task<IActionResult> EjecutarOperacion(
            Func<Task<IActionResult>> accion,
            string operacion,
            CancellationToken cancellationToken)
        {
            try
            {
                return await accion();
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested ||
                (HttpContext?.RequestAborted.IsCancellationRequested ?? false))
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                return CrearProblema(
                    StatusCodes.Status400BadRequest,
                    "Solicitud inválida",
                    exception.Message);
            }
            catch (KeyNotFoundException exception)
            {
                return CrearProblema(
                    StatusCodes.Status404NotFound,
                    "Recurso no encontrado",
                    exception.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error al {Operacion}. TraceId: {TraceId}",
                    operacion,
                    HttpContext?.TraceIdentifier ?? "N/A");

                return CrearProblema(
                    StatusCodes.Status500InternalServerError,
                    "Error interno del servidor",
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

            return StatusCode(estado, problema);
        }

    }
}
