using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/juegos/tesoro")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SwaggerResponse(401, "Requiere iniciar sesión.")]
[SwaggerResponse(400, "Vista o reto inválido.", typeof(ProblemDetails))]
public sealed class JuegoTesoroController(IJuegoTesoroServicio servicio) : ControllerBase
{
    [HttpGet("actual")]
    [SwaggerOperation(Summary = "Consulta la pista semanal y el hallazgo del usuario.",
        Description = "La semana comienza el domingo en Argentina. No revela el nombre del lugar ni sus coordenadas.")]
    [SwaggerResponse(200, "Pista semanal.", typeof(RetoTesoroDto))]
    [SwaggerResponse(204, "No hay retos habilitados.")]
    public async Task<IActionResult> Actual(CancellationToken ct) =>
        await servicio.ObtenerActual(User.ObtenerUsuarioIdRequerido(), ct) is { } reto ? Ok(reto) : NoContent();

    [HttpGet("visible")]
    [SwaggerOperation(Summary = "Busca el tesoro en la vista actual del mapa.",
        Description = "Sólo entrega coordenadas con zoom suficiente, dentro de la vista y sin hallazgo previo. No exige inclinación.")]
    [SwaggerResponse(200, "Tesoro visible.", typeof(TesoroVisibleDto))]
    [SwaggerResponse(204, "No hay tesoro visible en esta vista.")]
    public async Task<IActionResult> Visible([FromQuery] VistaMapaTesoroDto vista, CancellationToken ct) =>
        await servicio.ObtenerVisible(User.ObtenerUsuarioIdRequerido(), vista, ct) is { } tesoro ? Ok(tesoro) : NoContent();

    [HttpPost("{retoId:int}/hallazgo")]
    [SwaggerOperation(Summary = "Registra el hallazgo del tesoro semanal.",
        Description = "Usa al usuario autenticado y comprueba semana, zoom y vista. Los reintentos devuelven el mismo hallazgo; no entrega puntos.")]
    [SwaggerResponse(200, "Hallazgo guardado.", typeof(HallazgoTesoroDto))]
    public Task<HallazgoTesoroDto> Recoger(int retoId, [FromBody] VistaMapaTesoroDto vista, CancellationToken ct) =>
        servicio.Recoger(User.ObtenerUsuarioIdRequerido(), retoId, vista, ct);
}
