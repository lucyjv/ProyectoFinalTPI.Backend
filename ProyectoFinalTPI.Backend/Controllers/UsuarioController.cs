using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Extensions;
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
    [SwaggerResponse(StatusCodes.Status200OK, "Usuario encontrado.", typeof(UsuarioResumenDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El recurso solicitado no existe o no está disponible.", typeof(ProblemDetails))]
    public async Task<IActionResult> ObtenerPorUsername(
        string username, CancellationToken cancellationToken)
    {
        // KeyNotFoundException → 404 lo maneja ManejoExcepcionesMiddleware automáticamente.
        var resultado = await _usuarioServicio.ObtenerPorUsernameAsync(username, cancellationToken);
        return Ok(resultado);
    }

    [HttpGet("perfil/{username}")]
    [SwaggerOperation(
        Summary = "Obtiene el perfil completo de un usuario por su username.",
        Description = "Requiere autenticación. Devuelve la información del perfil del usuario, contadores y relación de seguimiento."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Perfil del usuario.", typeof(UsuarioPerfilDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario no existe.", typeof(ProblemDetails))]
    public async Task<IActionResult> ObtenerPerfilPorUsername(
        string username, CancellationToken cancellationToken)
    {
        var usuarioAutenticadoId = User.ObtenerUsuarioId();
        var resultado = await _usuarioServicio.ObtenerPerfilPorUsernameAsync(username, usuarioAutenticadoId, cancellationToken);
        return Ok(resultado);
    }

    [HttpGet("{id:int}/perfil")]
    [SwaggerOperation(
        Summary = "Obtiene el perfil completo de un usuario por su ID.",
        Description = "Requiere autenticación. Devuelve la información del perfil del usuario, contadores y relación de seguimiento."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Perfil del usuario.", typeof(UsuarioPerfilDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario no existe.", typeof(ProblemDetails))]
    public async Task<IActionResult> ObtenerPerfilPorId(
        int id, CancellationToken cancellationToken)
    {
        var usuarioAutenticadoId = User.ObtenerUsuarioId();
        var resultado = await _usuarioServicio.ObtenerPerfilPorIdAsync(id, usuarioAutenticadoId, cancellationToken);
        return Ok(resultado);
    }
}
