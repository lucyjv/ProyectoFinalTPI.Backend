using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/categorias-cuenta")]
public class CategoriasCuentaController(
    ICategoriaServicio servicio) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [SwaggerOperation(
        Summary = "Consulta el catálogo de categorías y permisos.",
        Description =
            "Devuelve las definiciones acordadas. Consultar este catálogo " +
            "no asigna categorías ni habilita o bloquea acciones.")]
    [SwaggerResponse(
        StatusCodes.Status200OK,
        "Catálogo de categorías.",
        typeof(IReadOnlyList<CategoriaCuentaDto>))]
    public IReadOnlyList<CategoriaCuentaDto> Obtener() =>
        servicio.ObtenerCatalogo();
}