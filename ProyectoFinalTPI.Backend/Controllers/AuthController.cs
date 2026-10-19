using Swashbuckle.AspNetCore.Annotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Extensions;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/auth")]
[SwaggerResponse(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al procesar la operación.", typeof(ProblemDetails))]
public class AuthController : ControllerBase
{
    public const string RefreshCookieName = "__Host-Nostalgiar.Refresh";
    private readonly IAuthServicio _authServicio;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthServicio authServicio, IWebHostEnvironment environment)
    {
        _authServicio = authServicio;
        _environment = environment;
    }

    [HttpPost("registro")]
    [EnableRateLimiting("auth-intentos")]
    [SwaggerOperation(
        Summary = "Registra una cuenta.",
        Description = "Recibe los datos del usuario, devuelve el token de acceso y establece la cookie HttpOnly de renovación. Fuera de Development y Testing requiere X-Nostalgiar-Request: 1."
    )]
    [SwaggerResponse(StatusCodes.Status201Created, "Cuenta creada y sesión iniciada.", typeof(AuthRespuestaDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Ya existe una cuenta con el email indicado.")]
    [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "Formato de contenido no compatible; utilice application/json.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Se superó el límite de intentos. Espere antes de reintentar.")]
    public async Task<IActionResult> Registro([FromBody] RegistroDto dto, CancellationToken cancellationToken)
    {
        if (!EsSolicitudDelFrontend()) return BadRequest(new { mensaje = "Solicitud de autenticación inválida." });

        try
        {
            var resultado = await _authServicio.RegistrarAsync(dto, UserAgent(), cancellationToken);
            EscribirRefreshCookie(resultado);
            return Created($"/api/usuarios/{resultado.Respuesta.UsuarioId}", resultado.Respuesta);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-intentos")]
    [SwaggerOperation(
        Summary = "Inicia sesión.",
        Description = "Valida email y contraseña, devuelve el token de acceso y establece la cookie HttpOnly de renovación. Fuera de Development y Testing requiere X-Nostalgiar-Request: 1."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Sesión iniciada.", typeof(AuthRespuestaDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Email o contraseña incorrectos.")]
    [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "Formato de contenido no compatible; utilice application/json.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Se superó el límite de intentos. Espere antes de reintentar.")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        if (!EsSolicitudDelFrontend()) return BadRequest(new { mensaje = "Solicitud de autenticación inválida." });

        try
        {
            var resultado = await _authServicio.LoginAsync(dto, UserAgent(), cancellationToken);
            EscribirRefreshCookie(resultado);
            return Ok(resultado.Respuesta);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { mensaje = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [SwaggerOperation(
        Summary = "Renueva la sesión.",
        Description = "Utiliza la cookie de renovación y la reemplaza por una nueva. Devuelve un nuevo token de acceso. Fuera de Development y Testing requiere X-Nostalgiar-Request: 1."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Sesión renovada.", typeof(AuthRespuestaDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "La cookie no existe o la sesión es inválida, expiró o ya fue renovada.")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!EsSolicitudDelFrontend()) return BadRequest(new { mensaje = "Solicitud de autenticación inválida." });
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
            return Unauthorized(new { mensaje = "No hay una sesión para renovar." });

        try
        {
            var resultado = await _authServicio.RenovarAsync(refreshToken, cancellationToken);
            EscribirRefreshCookie(resultado);
            return Ok(resultado.Respuesta);
        }
        catch (UnauthorizedAccessException ex)
        {
            BorrarRefreshCookie();
            return Unauthorized(new { mensaje = ex.Message });
        }
    }

    [HttpPost("logout")]
    [SwaggerOperation(
        Summary = "Cierra la sesión actual.",
        Description = "Revoca la sesión asociada a la cookie de renovación y elimina esa cookie. Fuera de Development y Testing requiere X-Nostalgiar-Request: 1."
    )]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Sesión cerrada; no devuelve contenido.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!EsSolicitudDelFrontend()) return BadRequest(new { mensaje = "Solicitud de autenticación inválida." });
        Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
        await _authServicio.CerrarSesionAsync(refreshToken, cancellationToken);
        BorrarRefreshCookie();
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-todos")]
    [SwaggerOperation(
        Summary = "Cierra todas las sesiones del usuario.",
        Description = "Requiere autenticación. Revoca todas las sesiones del usuario y elimina la cookie de renovación. Fuera de Development y Testing requiere X-Nostalgiar-Request: 1."
    )]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Sesiones cerradas; no devuelve contenido.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    public async Task<IActionResult> LogoutTodos(CancellationToken cancellationToken)
    {
        if (!EsSolicitudDelFrontend()) return BadRequest(new { mensaje = "Solicitud de autenticación inválida." });
        if (!TryObtenerUsuarioId(out var usuarioId)) return Unauthorized();
        await _authServicio.CerrarTodasLasSesionesAsync(usuarioId, cancellationToken);
        BorrarRefreshCookie();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [SwaggerOperation(
        Summary = "Obtiene el usuario autenticado.",
        Description = "Devuelve identificador, username, email y condición de administrador a partir de los claims del token."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Datos del usuario autenticado.", typeof(UsuarioAutenticadoDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticación válida.")]
    public IActionResult Me()
    {
        if (!TryObtenerUsuarioId(out var usuarioId)) return Unauthorized();
        return Ok(new UsuarioAutenticadoDto
        {
            UsuarioId = usuarioId,
            Username = User.FindFirstValue("username") ?? string.Empty,
            Email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty,
            EsAdmin = bool.TryParse(User.FindFirstValue("esAdmin"), out var esAdmin) && esAdmin
        });
    }

    private bool EsSolicitudDelFrontend() =>
        _environment.EnvironmentName == "Testing" || _environment.IsDevelopment() || Request.Headers["X-Nostalgiar-Request"] == "1";

    private string? UserAgent() => Request.Headers.UserAgent.ToString();

    private bool TryObtenerUsuarioId(out int usuarioId)
    {
        var id = User.ObtenerUsuarioId();
        usuarioId = id.GetValueOrDefault();
        return id.HasValue;
    }

    private void EscribirRefreshCookie(ResultadoAutenticacionDto resultado) =>
        Response.Cookies.Append(RefreshCookieName, resultado.RefreshToken, CookieOptions(resultado.RefreshTokenExpira));

    private void BorrarRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, CookieOptions(DateTimeOffset.UnixEpoch.UtcDateTime));

    private CookieOptions CookieOptions(DateTime expira) => new()
    {
        HttpOnly = true,
        Secure = true,
        // Next dev usa HTTP y la API HTTPS: en desarrollo son sitios distintos por esquema.
        // El encabezado anti-CSRF y CORS con orígenes explícitos protegen este flujo.
        SameSite = _environment.IsDevelopment() ? SameSiteMode.None : SameSiteMode.Strict,
        Path = "/",
        Expires = new DateTimeOffset(DateTime.SpecifyKind(expira, DateTimeKind.Utc))
    };
}
