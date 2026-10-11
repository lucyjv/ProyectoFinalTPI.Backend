using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
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
                .Where(p => !p.EstaOculto)
                .ToListAsync();

            var result = publicaciones.Select(p => new {
                id = p.Id.ToString(),
                title = p.Titulo,
                year = p.Fecha.Year,
                author = p.Usuario?.Username ?? "Desconocido",
                place = p.Lugar?.Nombre ?? "",
                lat = p.Lugar?.Coordenadas.Y ?? 0,
                lng = p.Lugar?.Coordenadas.X ?? 0,
                description = p.Descripcion,
                category = p.Categoria.ToString().Replace("Recuerdos_Personales", "Personales"),
                source = "db",
                image = p.TipoMultimedia == MultimediaEnum.Foto ? p.UrlMultimedia : null,
                media = (!string.IsNullOrEmpty(p.UrlMultimedia) && p.TipoMultimedia != MultimediaEnum.Foto) ? new {
                    url = p.UrlMultimedia,
                    kind = p.TipoMultimedia == MultimediaEnum.Video ? "video" : "image"
                } : null
            });

            return Ok(result);
        }

        private int? ObtenerUsuarioIdAutenticado()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value;

            return int.TryParse(idString, out var id) ? id : null;
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromForm] CrearPublicacionRequest request)
        {
            var autorIdAutenticado = ObtenerUsuarioIdAutenticado();
            var autorIdFinal = autorIdAutenticado ?? request.AutorId;

            if (autorIdFinal <= 0)
            {
                return Unauthorized(new { mensaje = "Debe iniciar sesión para crear una publicación." });
            }

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

            try
            {
                var id = await _publicacionServicio.Crear(dto);

                return Created(
                    $"/api/publicaciones/{id}",
                    new { id });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mensaje = ex.Message });
            }
        }

        [HttpPost("compartir")]
        public async Task<IActionResult> Compartir([FromBody] CompartirPublicacionRequest request)
        {
            var autorIdAutenticado = ObtenerUsuarioIdAutenticado();
            var autorIdFinal = autorIdAutenticado ?? request.AutorId;

            if (autorIdFinal <= 0)
            {
                return Unauthorized(new { mensaje = "Debe iniciar sesión para compartir una publicación." });
            }

            var dto = new CompartirPublicacionDto
            {
                PublicacionOriginalId = request.PublicacionOriginalId,
                AutorId = autorIdFinal,
                Descripcion = request.Descripcion ?? string.Empty
            };

            try
            {
                var id = await _publicacionServicio.Compartir(dto);

                return Created(
                    $"/api/publicaciones/{id}",
                    new { id });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mensaje = ex.Message });
            }
        }

        [HttpPost("{publicacionId:int}/etiquetas")]
        public async Task<IActionResult> EtiquetarUsuarios(
            int publicacionId, [FromBody] EtiquetarUsuariosRequest request, CancellationToken cancellationToken)
        {
            try
            {//valida los IDs y ejecuta la consulta Cypher.
                var cantidadEtiquetasNuevas = await _etiquetadoPublicacionServicio.EtiquetarUsuarios( publicacionId, request.UsuariosIds, cancellationToken);

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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { error = exception.Message });
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { error = exception.Message });
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error al etiquetar usuarios en la publicacion {PublicacionId}.",
                    publicacionId);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { error = "Ocurrio un error al etiquetar usuarios en la publicacion" });
            }
        }
    }
}

