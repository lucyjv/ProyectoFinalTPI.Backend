using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize(Policy = "AdministrarPanel")]
[Route("api/admin/juegos/tesoro")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SwaggerResponse(401, "Requiere iniciar sesión.")]
[SwaggerResponse(403, "Requiere permisos de administrador.")]
[SwaggerResponse(400, "Ubicación o semana inválida.", typeof(ProblemDetails))]
[SwaggerResponse(404, "Ubicación inexistente.", typeof(ProblemDetails))]
public sealed class AdminJuegoTesoroController(IAdminJuegoTesoroServicio servicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Lista ubicaciones y la planificación de las próximas doce semanas.")]
    [SwaggerResponse(200, "Panel del tesoro.", typeof(PanelTesoroAdminDto))]
    public Task<PanelTesoroAdminDto> Obtener(CancellationToken ct) => servicio.ObtenerPanel(User.ObtenerUsuarioIdRequerido(), ct);

    [HttpPost("ubicaciones")]
    [SwaggerOperation(Summary = "Crea una ubicación con su pista y zoom mínimo.")]
    [SwaggerResponse(201, "Ubicación creada.", typeof(UbicacionTesoroDto))]
    public async Task<IActionResult> Crear([FromBody] GuardarUbicacionTesoroDto dto, CancellationToken ct) =>
        StatusCode(201, await servicio.GuardarUbicacion(User.ObtenerUsuarioIdRequerido(), null, dto, ct));

    [HttpPut("ubicaciones/{id:int}")]
    [SwaggerOperation(Summary = "Edita o desactiva una ubicación sin modificar el reto vigente ni sus hallazgos.")]
    [SwaggerResponse(200, "Ubicación actualizada.", typeof(UbicacionTesoroDto))]
    public Task<UbicacionTesoroDto> Actualizar(int id, [FromBody] GuardarUbicacionTesoroDto dto, CancellationToken ct) =>
        servicio.GuardarUbicacion(User.ObtenerUsuarioIdRequerido(), id, dto, ct);

    [HttpPut("semanas/{semana}")]
    [SwaggerOperation(Summary = "Programa una semana futura. Una ubicación nula pausa esa semana.")]
    [SwaggerResponse(204, "Programación guardada.")]
    public async Task<IActionResult> Programar(DateOnly semana, [FromBody] ProgramarTesoroDto dto, CancellationToken ct)
    {
        await servicio.Programar(semana, dto.UbicacionId, ct);
        return NoContent();
    }

    [HttpDelete("semanas/{semana}")]
    [SwaggerOperation(Summary = "Restablece la rotación automática de una semana futura.")]
    [SwaggerResponse(204, "Rotación automática restablecida.")]
    public async Task<IActionResult> UsarRotacion(DateOnly semana, CancellationToken ct)
    {
        await servicio.UsarRotacion(semana, ct);
        return NoContent();
    }

    [HttpPut("actual/{retoId:int}")]
    [SwaggerOperation(Summary = "Modifica el reto vigente conservando su ID y todos los hallazgos registrados.",
        Description = "Puede cambiar la pista, el lugar y el zoom para quienes aún no lo encontraron. No modifica el catálogo ni retos anteriores.")]
    [SwaggerResponse(204, "Reto vigente actualizado.")]
    public async Task<IActionResult> EditarActual(int retoId, [FromBody] EditarRetoTesoroDto dto, CancellationToken ct)
    {
        await servicio.EditarActual(User.ObtenerUsuarioIdRequerido(), retoId, dto, ct);
        return NoContent();
    }
}
