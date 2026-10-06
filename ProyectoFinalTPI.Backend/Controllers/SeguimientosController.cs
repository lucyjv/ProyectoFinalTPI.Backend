using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Models;
using ProyectoFinalTPI.Backend.Services;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/seguimientos")]
    public class SeguimientosController : ControllerBase
    {
        private readonly ISeguimientoService _seguimientoService;

        public SeguimientosController(ISeguimientoService seguimientoService)
        {
            _seguimientoService = seguimientoService;
        }

        [HttpPost]
        public async Task<IActionResult> SeguirUsuario(
            [FromBody] SolicitudSeguirUsuario solicitud,
            CancellationToken cancellationToken)
        {
            var resultado = await _seguimientoService.SeguirUsuarioAsync(
                solicitud.IdUsuario,
                solicitud.IdUsuarioASeguir,
                cancellationToken);

            return resultado switch
            {
                ResultadoSeguirUsuario.Seguido => Ok(new
                {
                    mensaje = "El usuario comenzó a seguir al otro usuario."
                }),
                ResultadoSeguirUsuario.YaLoSeguía => Ok(new
                {
                    mensaje = "El usuario ya seguía a ese usuario."
                }),
                ResultadoSeguirUsuario.AutoSeguimiento => BadRequest(new
                {
                    error = "Un usuario no puede seguirse a sí mismo."
                }),
                ResultadoSeguirUsuario.UsuarioNoEncontrado => NotFound(new
                {
                    error = "No se encontró uno o ambos usuarios."
                }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
    }
}
