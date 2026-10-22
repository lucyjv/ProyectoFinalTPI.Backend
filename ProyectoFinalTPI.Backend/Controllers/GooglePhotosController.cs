using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using ProyectoFinalTPI.Backend.Services;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/integraciones/google-photos")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GooglePhotosController(GooglePhotosService photos) : ControllerBase
{
    private const string BindingCookie = "nostalgiar.google-photos";
    private int Owner()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(value, out var id) && id > 0 ? id
            : throw new GooglePhotosException(401, "Iniciá sesión en Nostalgiar para importar fotos.");
    }

    [HttpPost("sesiones")]
    public Task<IActionResult> Start() => Run(() =>
    {
        var binding = Request.Cookies[BindingCookie] ?? WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var result = photos.Start(Owner(), binding);
        Response.Cookies.Append(BindingCookie, binding, new CookieOptions
        {
            HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax,
            Path = "/api/integraciones/google-photos", MaxAge = TimeSpan.FromMinutes(20), IsEssential = true
        });
        return Task.FromResult<IActionResult>(Ok(result));
    });

    [AllowAnonymous]
    [HttpGet("callback")]
    public Task<IActionResult> Callback([FromQuery] string? state, [FromQuery] string? code,
        [FromQuery] string? error, CancellationToken ct) => Run(async () =>
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var picker = await photos.Callback(state ?? "", Request.Cookies[BindingCookie], code, error, ct);
        return Redirect(picker);
    });

    [HttpGet("sesiones/{id}")]
    public Task<IActionResult> Status(string id, CancellationToken ct) => Run(async () =>
        Ok(await photos.Status(id, Owner(), ct)));

    // Returns a file for the existing multipart publication flow. Nothing is published here.
    [HttpPost("sesiones/{id}/imagen")]
    public Task<IActionResult> Import(string id, CancellationToken ct) => Run(async () =>
    {
        var image = await photos.Import(id, Owner(), ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(image.Bytes, image.ContentType, image.FileName);
    });

    [HttpDelete("sesiones/{id}")]
    public Task<IActionResult> Cancel(string id, CancellationToken ct) => Run(async () =>
    {
        await photos.Cancel(id, Owner(), ct);
        return NoContent();
    });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (GooglePhotosException ex) { return Problem(statusCode: ex.Status, title: "Google Photos", detail: ex.Message); }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        { return Problem(statusCode: 504, title: "Google Photos", detail: "Google tardó demasiado en responder. Volvé a intentar."); }
        catch (HttpRequestException)
        { return Problem(statusCode: 502, title: "Google Photos", detail: "No se pudo conectar con Google Photos."); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        { return Problem(statusCode: 502, title: "Google Photos", detail: "Google Photos devolvió una respuesta inesperada."); }
    }
}
