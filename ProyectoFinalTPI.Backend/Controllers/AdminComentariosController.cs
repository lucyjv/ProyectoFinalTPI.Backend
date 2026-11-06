using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Authorize(Policy = "AdministrarPanel")]
[Route("api/admin/comentarios")]
[SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere autenticación.")]
[SwaggerResponse(StatusCodes.Status403Forbidden, "Se requieren permisos de administrador.")]
public class AdminComentariosController(IAdminComentarioServicio servicio) : ControllerBase
{
    [HttpDelete("{id:int}")]
    [SwaggerOperation(Summary = "Elimina un comentario.", Description = "Realiza una eliminación lógica, conserva el hilo y notifica al autor.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Comentario eliminado.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El comentario no existe.", typeof(ProblemDetails))]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken)
    {
        await servicio.Eliminar(id, cancellationToken);
        return NoContent();
    }
}
