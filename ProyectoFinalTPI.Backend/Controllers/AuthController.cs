using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthServicio _authServicio;

        public AuthController(IAuthServicio authServicio)
        {
            _authServicio = authServicio;
        }

        [HttpPost("registro")]
        public async Task<IActionResult> Registro([FromBody] RegistroDto dto)
        {
            try
            {
                var respuesta = await _authServicio.RegistrarAsync(dto);
                return Created($"/api/usuarios/{respuesta.UsuarioId}", respuesta);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { mensaje = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var respuesta = await _authServicio.LoginAsync(dto);
                return Ok(respuesta);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Siempre el mismo mensaje hacia afuera: no revelar si el email existe o no.
                return Unauthorized(new { mensaje = ex.Message });
            }
        }
    }
}
