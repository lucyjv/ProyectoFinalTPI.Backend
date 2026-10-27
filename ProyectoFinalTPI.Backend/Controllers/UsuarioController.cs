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

    [HttpGet("me/perfil")]
    [SwaggerOperation(Summary = "Obtiene el perfil propio actualizado.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Perfil propio.", typeof(UsuarioPerfilDto))]
    public async Task<IActionResult> ObtenerMiPerfil(CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        return Ok(await _usuarioServicio.ObtenerPerfilPorIdAsync(usuarioId.Value, usuarioId, cancellationToken));
    }

    [HttpPatch("me/username")]
    [SwaggerOperation(Summary = "Cambia el nombre de usuario de la cuenta autenticada.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Perfil actualizado.", typeof(UsuarioPerfilDto))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "El nombre de usuario ya existe.", typeof(ProblemDetails))]
    public async Task<IActionResult> CambiarMiUsername(
        [FromBody] CambiarUsernameDto dto, CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        return Ok(await _usuarioServicio.CambiarUsernamePropioAsync(usuarioId.Value, dto.Username, cancellationToken));
    }

    [HttpPut("me/foto")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [SwaggerOperation(Summary = "Sube o reemplaza la foto de perfil de la cuenta autenticada.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Perfil actualizado.", typeof(UsuarioPerfilDto))]
    public async Task<IActionResult> CambiarMiFoto(
        IFormFile archivo, CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { mensaje = "Seleccioná una foto." });
        if (archivo.Length > 2 * 1024 * 1024)
            return BadRequest(new { mensaje = "La foto no puede superar 2 MiB." });
        await using var contenido = archivo.OpenReadStream();
        return Ok(await _usuarioServicio.CambiarFotoPerfilPropiaAsync(
            usuarioId.Value, contenido, archivo.ContentType, cancellationToken));
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

    [HttpGet("buscar")]
    [SwaggerOperation(
        Summary = "Busca usuarios por username.",
        Description = "Requiere autenticación. Devuelve hasta 20 coincidencias parciales, sin distinguir mayúsculas."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Usuarios encontrados.", typeof(IReadOnlyList<AutorResumenDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los parámetros de búsqueda no son válidos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    public async Task<IActionResult> Buscar(
        [FromQuery] string texto,
        [FromQuery] int limite = 10,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _usuarioServicio.BuscarAsync(texto, limite, cancellationToken);
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
