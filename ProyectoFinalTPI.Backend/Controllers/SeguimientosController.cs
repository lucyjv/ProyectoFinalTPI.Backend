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
        public async Task<IActionResult> SeguirUsuario(
            [FromBody] SeguirUsuarioRequest request,
            CancellationToken cancellationToken) // cancelar una operación que está en curso
                                                 // si el cliente cierra la conexion o deja de esperar la respuesta,.NET puede señalar esa cancelacion
        {
            try
            {
                var resultado = await _seguimientoServicio.SeguirUsuarioAsync(
                    request.IdUsuario,
                    request.IdUsuarioASeguir,
                    cancellationToken);

                return resultado switch
                {
                    ResultadoSeguirUsuario.Seguido => StatusCode( StatusCodes.Status201Created, new { mensaje = "Ahora seguís a este usuario." }),
                    ResultadoSeguirUsuario.YaLoSeguía => Ok(new{ mensaje = "Ya seguías a este usuario." }),
                    ResultadoSeguirUsuario.AutoSeguimiento => BadRequest(new{ error = "No podés seguirte a vos mismo."}),
                    ResultadoSeguirUsuario.UsuarioNoEncontrado => NotFound(new{ error = "No se encontró uno o ambos usuarios."}),
                    _ => StatusCode( StatusCodes.Status500InternalServerError, new { error = "Ocurrió un error al procesar el seguimiento." })
                 // _ para cualquier otro valor
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error al registrar el seguimiento entre los usuarios {IdUsuario} y {IdUsuarioASeguir}.",
                    request.IdUsuario,
                    request.IdUsuarioASeguir);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,new { error = "Ocurrió un error al procesar el seguimiento." });
            }
        }
    }
}
