using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/auth")]
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
        _environment.EnvironmentName == "Testing" || Request.Headers["X-Nostalgiar-Request"] == "1";

    private string? UserAgent() => Request.Headers.UserAgent.ToString();

    private bool TryObtenerUsuarioId(out int usuarioId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub), out usuarioId);

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
