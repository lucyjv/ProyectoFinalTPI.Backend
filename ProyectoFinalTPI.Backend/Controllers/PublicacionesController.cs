using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;
using ProyectoFinalTPI.Backend.Mappings;
using ProyectoFinalTPI.Backend.Extensions;

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

        public PublicacionesController(
            IPublicacionServicio publicacionServicio,
            IEtiquetadoPublicacionServicio etiquetadoPublicacionServicio)
        {
            _publicacionServicio = publicacionServicio;
            _etiquetadoPublicacionServicio = etiquetadoPublicacionServicio;
        }

        [HttpGet("mapa")]
        [ProducesResponseType(typeof(ResultadoMapaDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
            await _publicacionServicio.CompletarResumenesLikes(
                [detalle], User.ObtenerUsuarioId() ?? 0, cancellationToken);

            return Ok(detalle);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Crear([FromForm] CrearPublicacionRequest request)
        {
            var autorIdAutenticado = User.ObtenerUsuarioId();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : request.AutorId;

            using var stream = request.Archivo?.OpenReadStream();

            var id = await _publicacionServicio.Crear(
                request.ToPublicacion(autorIdFinal), stream, request.Archivo?.FileName);

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
            var autorIdAutenticado = User.ObtenerUsuarioId();
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
            var autorIdAutenticado = User.ObtenerUsuarioId();
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
            var autorIdAutenticado = User.ObtenerUsuarioId();
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

        [Authorize]
        [HttpPost("{publicacionId:int}/like")]
        [ProducesResponseType(typeof(MeGustaPublicacionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<MeGustaPublicacionDto>> DarLike(
            int publicacionId, CancellationToken cancellationToken = default)
        {
            var usuarioId = User.ObtenerUsuarioId();
            if (!usuarioId.HasValue || usuarioId.Value <= 0) return Unauthorized();

            return Ok(await _publicacionServicio.DarLike( publicacionId, usuarioId.Value, cancellationToken));
        }

        [Authorize]
        [HttpDelete("{publicacionId:int}/like")]
        [ProducesResponseType(typeof(MeGustaPublicacionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<MeGustaPublicacionDto>> QuitarLike(
            int publicacionId, CancellationToken cancellationToken = default)
        {
            var usuarioId = User.ObtenerUsuarioId();
            if (!usuarioId.HasValue || usuarioId.Value <= 0) return Unauthorized();

            return Ok(await _publicacionServicio.QuitarLike( publicacionId, usuarioId.Value, cancellationToken));
        }



    }
}
