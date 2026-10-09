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
    public class PublicacionesController : ControllerBase
    {
        private readonly IPublicacionServicio _publicacionServicio;

        public PublicacionesController(
            IPublicacionServicio publicacionServicio)
        {
            _publicacionServicio = publicacionServicio;
        }

        [HttpGet("mapa")]
        public Task<ActionResult<ResultadoMapaDto>> ObtenerMapa(
            [FromQuery] PublicacionFiltroDto filtro,
            CancellationToken cancellationToken)
        {
            return EjecutarConsulta(() => _publicacionServicio.ObtenerPines(
                filtro, cancellationToken));
        }

        [HttpGet("{id:int}/resumen")]
        public Task<ActionResult<PublicacionResumenDto>> ObtenerResumen(
            int id,
            CancellationToken cancellationToken)
        {
            return EjecutarConsulta(() => _publicacionServicio.ObtenerResumenPorId(
                id, cancellationToken));
        }

        [HttpGet("{id:int}")]
        public Task<ActionResult<PublicacionDetalleDto>> ObtenerDetalle(
            int id,
            CancellationToken cancellationToken)
        {
            return EjecutarConsulta(() => _publicacionServicio.ObtenerDetallePorId(
                id, cancellationToken));
        }

        [HttpGet("{id:int}/comentarios")]
        public Task<ActionResult<ResultadoPaginadoDto<ComentarioDto>>> ObtenerComentarios(
            int id,
            [FromQuery] string? cursor = null,
            [FromQuery] int limite = 20,
            CancellationToken cancellationToken = default)
        {
            return EjecutarConsulta(() => _publicacionServicio.ObtenerComentarios(
                id, cursor, limite, cancellationToken));
        }

        private async Task<ActionResult<T>> EjecutarConsulta<T>(Func<Task<T>> consulta)
        {
            try
            {
                return Ok(await consulta());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mensaje = ex.Message });
            }
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
    }
}
