using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize(Policy = "AdministrarPanel")]
[Route("api/admin/usuarios")]
[SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
[SwaggerResponse(StatusCodes.Status403Forbidden, "Se requieren permisos de administrador.")]
public class AdminUsuariosController(IAdminUsuarioServicio servicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Lista usuarios para administración.", Description = "Busca por username, nombre, apellido o email y permite filtrar por estado.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Página de usuarios.", typeof(PaginaAdminDto<UsuarioAdminDto>))]
    public Task<PaginaAdminDto<UsuarioAdminDto>> Listar(
        [FromQuery] FiltroUsuariosAdminDto filtro, CancellationToken cancellationToken) =>
        servicio.Listar(filtro, cancellationToken);

    [HttpPatch("{id:int}")]
    [SwaggerOperation(Summary = "Suspende o reactiva un usuario.", Description = "Al suspender revoca todas las sesiones activas del usuario.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Estado actualizado.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario no existe.", typeof(ProblemDetails))]
    public async Task<IActionResult> CambiarSuspension(
        int id, [FromBody] CambiarSuspensionUsuarioDto dto, CancellationToken cancellationToken)
    {
        await servicio.CambiarSuspension(id, ObtenerAdminId(), dto, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [SwaggerOperation(Summary = "Da de baja un usuario.", Description = "Realiza una baja lógica y revoca todas sus sesiones sin eliminar su contenido.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Usuario dado de baja.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario no existe.", typeof(ProblemDetails))]
    public async Task<IActionResult> DarDeBaja(int id, CancellationToken cancellationToken)
    {
        await servicio.DarDeBaja(id, ObtenerAdminId(), cancellationToken);
        return NoContent();
    }

    private int ObtenerAdminId() => User.ObtenerUsuarioId()
        ?? throw new UnauthorizedAccessException("No se pudo identificar al administrador.");
}
