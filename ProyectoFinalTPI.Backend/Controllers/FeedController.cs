using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/feed")]
    public class FeedController : Controller
    {

        private readonly IPublicacionServicio _publicacionServicio;

        public FeedController(IPublicacionServicio publicacionServicio)
        {
            _publicacionServicio = publicacionServicio;
        }
        [HttpGet("para-vos")]
        public Task<ActionResult<ResultadoPaginadoDto<PublicacionDetalleDto>>>ObtenerParaVos(
                [FromQuery] int usuarioId,
                [FromQuery] string? cursor = null,
                [FromQuery] int limite = 20,
                CancellationToken cancellationToken = default)
        {
            return EjecutarConsulta(
                () => _publicacionServicio.ObtenerPublicacionesParaVos(
                    usuarioId,
                    cursor,
                    limite,
                    cancellationToken));
        }

        [HttpGet("seguidos")]
        public Task<ActionResult<ResultadoPaginadoDto<PublicacionDetalleDto>>>ObtenerSeguidos(
                [FromQuery] int usuarioId,
                [FromQuery] string? cursor = null,
                [FromQuery] int limite = 20,
                CancellationToken cancellationToken = default)
        {
            return EjecutarConsulta(
                () => _publicacionServicio.ObtenerFeedSeguidos(
                    usuarioId,
                    cursor,
                    limite,
                    cancellationToken));
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
