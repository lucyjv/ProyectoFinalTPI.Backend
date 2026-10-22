using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using ProyectoFinalTPI.Backend.Extensions;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/feed")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al procesar la operación.", typeof(ProblemDetails))]
    public class FeedController : ControllerBase
    {
        private readonly IPublicacionServicio _publicacionServicio;

        public FeedController(IPublicacionServicio publicacionServicio)
        {
            _publicacionServicio = publicacionServicio;
        }

        [HttpGet("para-vos")]
        [SwaggerOperation(
            Summary = "Obtiene el feed personalizado.",
            Description = "Devuelve publicaciones recomendadas con paginación por cursor y un límite de 1 a 50. Prioriza el usuario del token; admite usuarioId como alternativa. Si la sesión del cursor ya no está disponible, debe reiniciarse la consulta sin cursor."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Página de publicaciones recomendadas.", typeof(ResultadoPaginadoDto<PublicacionDetalleDto>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No se pudo identificar al usuario.")]
        [SwaggerResponse(StatusCodes.Status410Gone, "La sesión del feed no está disponible; reinicie la consulta sin cursor.")]
        public Task<ActionResult<ResultadoPaginadoDto<PublicacionDetalleDto>>> ObtenerParaVos(
                [FromQuery] int? usuarioId = null,
                [FromQuery] string? cursor = null,
                [FromQuery] int limite = 20,
                CancellationToken cancellationToken = default)
        {
            var idFinal = ObtenerUsuarioId(usuarioId);
            if (idFinal <= 0)
            {
                return Task.FromResult<ActionResult<ResultadoPaginadoDto<PublicacionDetalleDto>>>(
                    Unauthorized(new { mensaje = "Debés iniciar sesión para ver el feed personalizado." }));
            }

            return EjecutarConsulta(
                () => _publicacionServicio.ObtenerPublicacionesParaVos(
                    idFinal,
                    cursor,
                    limite,
                    cancellationToken));
        }

        [HttpGet("seguidos")]
        [SwaggerOperation(
            Summary = "Obtiene el feed de usuarios seguidos.",
            Description = "Devuelve publicaciones de usuarios seguidos, ordenadas cronológicamente y paginadas por cursor. El límite debe estar entre 1 y 50. Prioriza el usuario del token; admite usuarioId como alternativa."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Página de publicaciones de usuarios seguidos.", typeof(ResultadoPaginadoDto<PublicacionDetalleDto>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No se pudo identificar al usuario.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario indicado no existe.")]
        public Task<ActionResult<ResultadoPaginadoDto<PublicacionDetalleDto>>> ObtenerSeguidos(
                [FromQuery] int? usuarioId = null,
                [FromQuery] string? cursor = null,
                [FromQuery] int limite = 20,
                CancellationToken cancellationToken = default)
        {
            var idFinal = ObtenerUsuarioId(usuarioId);
            if (idFinal <= 0)
            {
                return Task.FromResult<ActionResult<ResultadoPaginadoDto<PublicacionDetalleDto>>>(
                    Unauthorized(new { mensaje = "Debés iniciar sesión para ver el feed de seguidos." }));
            }

            return EjecutarConsulta(
                () => _publicacionServicio.ObtenerFeedSeguidos(
                    idFinal,
                    cursor,
                    limite,
                    cancellationToken));
        }

        [HttpPost("impresiones")]
        [SwaggerOperation(
            Summary = "Registra impresiones del feed Para vos.",
            Description = "Actualiza en Neo4j la fecha de última impresión para cada publicación recibida. El usuario se identifica mediante el token."
        )]
        [SwaggerResponse(StatusCodes.Status204NoContent, "Impresiones registradas.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "La lista de publicaciones no es válida.")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una sesión autenticada.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario autenticado no existe.")]
        public async Task<IActionResult> RegistrarImpresiones(
            [FromBody] RegistrarImpresionesFeedRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
                return BadRequest(new { mensaje = "Debés enviar publicacionesIds." });

            var usuarioId = User.ObtenerUsuarioId();
            if (!usuarioId.HasValue || usuarioId.Value <= 0)
                return Unauthorized(new { mensaje = "Debés iniciar sesión para registrar impresiones." });

            try
            {
                await _publicacionServicio.RegistrarImpresionesFeed(
                    usuarioId.Value, request.PublicacionesIds ?? Array.Empty<int>(), cancellationToken);
                return NoContent();
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

        private int ObtenerUsuarioId(int? queryUsuarioId)
        {
            return User.ObtenerUsuarioId() ?? queryUsuarioId.GetValueOrDefault();
        }

        private async Task<ActionResult<ResultadoPaginadoDto<PublicacionDetalleDto>>> EjecutarConsulta(
            Func<Task<ResultadoPaginadoDto<PublicacionDetalleDto>>> consulta)
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
            catch (SesionFypNoDisponibleException ex)
            {
                return StatusCode(
                    StatusCodes.Status410Gone,
                    new
                    {
                        codigo = "SESION_FYP_NO_DISPONIBLE",
                        mensaje = ex.Message
                    });
            }
        }
    }
}
