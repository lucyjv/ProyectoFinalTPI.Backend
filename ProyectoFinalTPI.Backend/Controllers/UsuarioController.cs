using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/usuario")]
[Authorize]
public class UsuarioController : ControllerBase
{
    private readonly IUsuarioServicio _usuarioServicio;

    public UsuarioController(IUsuarioServicio usuarioServicio)
    {
        _usuarioServicio = usuarioServicio;
    }

    /// <summary>
    /// Devuelve el id y el username del usuario que coincide exactamente con el username buscado.
    /// </summary>
    /// <param name="username">Username exacto a buscar.</param>
    /// <response code="200">Usuario encontrado.</response>
    /// <response code="401">El usuario no está autenticado.</response>
    /// <response code="404">No existe ningún usuario con ese username.</response>
    [HttpGet("por-username/{username}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorUsername(
        string username, CancellationToken cancellationToken)
    {
        // KeyNotFoundException → 404 lo maneja ManejoExcepcionesMiddleware automáticamente.
        var resultado = await _usuarioServicio.ObtenerPorUsernameAsync(username, cancellationToken);
        return Ok(resultado);
    }
}
