using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/publicaciones")]
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

        [HttpPost]
        public async Task<IActionResult> Crear([FromForm] CrearPublicacionRequest request)
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
            var dto = new CompartirPublicacionDto
            {
                PublicacionOriginalId = request.PublicacionOriginalId,
                AutorId = request.AutorId,
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
