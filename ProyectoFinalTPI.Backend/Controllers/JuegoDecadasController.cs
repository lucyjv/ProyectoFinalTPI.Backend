using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.JuegoDecadas;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/juegos/decadas")]
public sealed class JuegoDecadasController(IJuegoDecadasServicio servicio) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("fotos")]
    [SwaggerOperation(
        Summary = "Obtiene fotos para el juego de las décadas.",
        Description = "Fotos de recuerdos originales y visibles de elNostalgico entre 1970 y 2029. " +
            "Excluye los títulos que contienen 'Expo Proyecto', sin distinguir mayúsculas. " +
            "La década corresponde a la fecha del recuerdo. No incluye videos ni guarda puntuaciones.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Fotos disponibles, o una lista vacía si no hay candidatas.",
        typeof(IReadOnlyList<FotoJuegoDecadasDto>))]
    public Task<IReadOnlyList<FotoJuegoDecadasDto>> ObtenerFotos(CancellationToken cancellationToken) =>
        servicio.ObtenerFotos(cancellationToken);
}
