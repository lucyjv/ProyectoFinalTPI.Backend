using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Seguimientos;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/seguimientos")]
    public class SeguimientosController : ControllerBase
    {
        private readonly ISeguimientoServicio _seguimientoServicio;
        private readonly ILogger<SeguimientosController> _logger;

        public SeguimientosController(
            ISeguimientoServicio seguimientoServicio,
            ILogger<SeguimientosController> logger)
        {
            _seguimientoServicio = seguimientoServicio;
            _logger = logger;
        }

        [HttpPost]
        public Task<IActionResult> SeguirUsuario(
            [FromBody] SeguirUsuarioRequest request,
            CancellationToken cancellationToken)
        {
            return EjecutarOperacion( () 
                => _seguimientoServicio.SeguirUsuarioAsync(
                    request.IdUsuario,
                    request.IdUsuarioASeguir,
                    cancellationToken),

                MapearResultadoSeguir,
                request.IdUsuario,
                request.IdUsuarioASeguir,
                cancellationToken,
                "crear");
        }

        [HttpDelete]
        public Task<IActionResult> DejarDeSeguirUsuario(
            [FromBody] DejarDeSeguirUsuarioRequest request, CancellationToken cancellationToken)
        {
            return EjecutarOperacion(
                () => _seguimientoServicio.DejarDeSeguirUsuarioAsync(
                    request.IdUsuario,
                    request.IdUsuarioASeguir,
                    cancellationToken),
                MapearResultadoDejarDeSeguir,
                request.IdUsuario,
                request.IdUsuarioASeguir,
                cancellationToken,
                "eliminar");
        }

        private IActionResult MapearResultadoSeguir(ResultadoSeguirUsuario resultado)
        {
            return resultado switch
            {
                ResultadoSeguirUsuario.Seguido => StatusCode( StatusCodes.Status201Created,new { mensaje = "Ahora seguís a este usuario." }),
                ResultadoSeguirUsuario.YaLoSeguía => Ok(new { mensaje = "Ya seguías a este usuario."}),
                ResultadoSeguirUsuario.AutoSeguimiento => BadRequest(new { error = "No podés seguirte a vos mismo." }),
                ResultadoSeguirUsuario.UsuarioNoEncontrado => NotFound(new { error = "No se encontró uno o ambos usuarios." }),
                
                _ => ErrorInterno()
            };
        }

        private IActionResult MapearResultadoDejarDeSeguir( ResultadoDejarDeSeguirUsuario resultado)
        {
            return resultado switch
            {
                ResultadoDejarDeSeguirUsuario.DejadoDeSeguir => NoContent(),
                ResultadoDejarDeSeguirUsuario.YaNoLoSeguía => NoContent(),
                ResultadoDejarDeSeguirUsuario.AutoSeguimiento => BadRequest(new { error = "No podés dejar de seguirte a vos mismo." }),
                ResultadoDejarDeSeguirUsuario.UsuarioNoEncontrado => NotFound(new { error = "No se encontró uno o ambos usuarios." }),
               
                _ => ErrorInterno()
            };
        }

        private async Task<IActionResult> EjecutarOperacion<TResult>(
            Func<Task<TResult>> ejecutar,
            Func<TResult, IActionResult> mapearResultado,
            int idUsuario,
            int idUsuarioASeguir,
            CancellationToken cancellationToken,
            string accion)
        {
            try
            {
                var resultado = await ejecutar();
                return mapearResultado(resultado);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error al {Accion} la relación de seguimiento entre los usuarios {IdUsuario} y {IdUsuarioASeguir}.",
                    accion,
                    idUsuario,
                    idUsuarioASeguir);

                return ErrorInterno();
            }
        }

        private IActionResult ErrorInterno()
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Ocurrió un error al procesar el seguimiento." });
        }
    }
}
