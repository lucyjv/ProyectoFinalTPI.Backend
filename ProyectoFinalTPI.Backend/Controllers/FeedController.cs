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
    public class FeedController : ControllerBase
    {
        private readonly IPublicacionServicio _publicacionServicio;

        public FeedController(IPublicacionServicio publicacionServicio)
        {
            _publicacionServicio = publicacionServicio;
        }

        [HttpGet("para-vos")]
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
