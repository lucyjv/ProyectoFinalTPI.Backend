using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/usuario")]
[Authorize]
[SwaggerResponse(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al procesar la operación.", typeof(ProblemDetails))]
public class UsuarioController : ControllerBase
{
    private readonly IUsuarioServicio _usuarioServicio;

    public UsuarioController(IUsuarioServicio usuarioServicio)
    {
        _usuarioServicio = usuarioServicio;
    }

    [HttpGet("por-username/{username}")]
    [SwaggerOperation(
        Summary = "Busca un usuario por su username.",
        Description = "Requiere autenticación. Busca una coincidencia exacta y devuelve el identificador y el username."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Usuario encontrado.", typeof(ProyectoFinalTPI.Backend.Dtos.Usuario.UsuarioResumenDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El recurso solicitado no existe o no está disponible.", typeof(ProblemDetails))]
    public async Task<IActionResult> ObtenerPorUsername(
        string username, CancellationToken cancellationToken)
    {
        // KeyNotFoundException → 404 lo maneja ManejoExcepcionesMiddleware automáticamente.
        var resultado = await _usuarioServicio.ObtenerPorUsernameAsync(username, cancellationToken);
        return Ok(resultado);
    }
}
