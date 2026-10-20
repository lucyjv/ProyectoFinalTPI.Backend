using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Grupo;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Mappings;
using ProyectoFinalTPI.Backend.Requests.Grupos;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/grupos")]
[SwaggerResponse(StatusCodes.Status500InternalServerError, "Ocurrio un error inesperado.", typeof(ProblemDetails))]
public class GruposController(IGrupoServicio grupoServicio) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Obtiene todos los grupos publicos.",
        Description = "Devuelve los grupos ordenados desde el mas reciente. No requiere autenticacion. Cuando existe un usuario autenticado, indica si es integrante o creador de cada grupo.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Listado de grupos publicos.", typeof(List<GrupoResumenDto>))]
    public async Task<ActionResult<IReadOnlyList<GrupoResumenDto>>> ObtenerTodos(
        CancellationToken cancellationToken = default)
    {
        return Ok(await grupoServicio.ObtenerTodos(User.ObtenerUsuarioId(), cancellationToken));
    }

    [HttpGet("buscar")]
    [SwaggerOperation(
        Summary = "Busca grupos publicos.",
        Description = "Busca coincidencias sin distinguir mayusculas y minusculas en el nombre y la descripcion. Prioriza los nombres que comienzan con el texto buscado y devuelve hasta 50 resultados.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Grupos que coinciden con la busqueda.", typeof(List<GrupoResumenDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El texto o el limite de resultados no son validos.", typeof(ValidationProblemDetails))]
    public async Task<ActionResult<IReadOnlyList<GrupoResumenDto>>> Buscar(
        [FromQuery] BuscarGruposRequest request,
        CancellationToken cancellationToken = default)
    {
        return Ok(await grupoServicio.Buscar(
            request.Texto,
            request.Limite,
            User.ObtenerUsuarioId(),
            cancellationToken));
    }

    [HttpGet("{grupoId:int}")]
    [SwaggerOperation(
        Summary = "Obtiene el detalle de un grupo.",
        Description = "Devuelve los datos publicos del grupo, su creador, cantidad de integrantes y la relacion del usuario autenticado con el grupo.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Detalle del grupo.", typeof(GrupoResumenDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador del grupo no es valido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El grupo no existe.", typeof(ProblemDetails))]
    public async Task<ActionResult<GrupoResumenDto>> ObtenerPorId(
        int grupoId, CancellationToken cancellationToken = default)
    {
        return Ok(await grupoServicio.ObtenerPorId(
            grupoId, User.ObtenerUsuarioId(), cancellationToken));
    }

    [Authorize]
    [HttpPost]
    [SwaggerOperation(
        Summary = "Crea un grupo publico.",
        Description = "Requiere autenticacion. Crea un grupo asociado a una decada, usa al usuario autenticado como creador y lo agrega automaticamente como primer integrante.")]
    [SwaggerResponse(StatusCodes.Status201Created, "Grupo creado. Devuelve el grupo y su ubicacion en Location.", typeof(GrupoResumenDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El nombre o la descripcion no son validos.", typeof(ValidationProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticacion valida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario autenticado ya no existe.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "Formato no compatible; utilice application/json.", typeof(ProblemDetails))]
    public async Task<IActionResult> Crear(
        [FromBody] CrearGrupoRequest request, CancellationToken cancellationToken = default)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        var grupo = await grupoServicio.Crear(request.ToGrupo(usuarioId.Value), cancellationToken);
        return Created($"/api/grupos/{grupo.Id}", grupo);
    }

    [Authorize]
    [HttpPut("{grupoId:int}")]
    [SwaggerOperation(
        Summary = "Actualiza un grupo.",
        Description = "Requiere autenticacion. Solo el creador puede modificar el nombre, la descripcion y la decada del grupo.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Grupo actualizado.", typeof(GrupoResumenDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador o los datos del grupo no son validos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticacion valida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "El usuario autenticado no es el creador del grupo.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El grupo no existe.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "Formato no compatible; utilice application/json.", typeof(ProblemDetails))]
    public async Task<ActionResult<GrupoResumenDto>> Actualizar(
        int grupoId, [FromBody] ActualizarGrupoRequest request,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        return Ok(await grupoServicio.Actualizar(
            grupoId, usuarioId.Value, request.ToGrupo(), cancellationToken));
    }

    [Authorize]
    [HttpDelete("{grupoId:int}")]
    [SwaggerOperation(
        Summary = "Elimina un grupo.",
        Description = "Requiere autenticacion. Solo el creador puede eliminarlo. Las membresias se eliminan y los recuerdos se conservan como publicaciones normales sin grupo.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Grupo eliminado.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador del grupo no es valido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticacion valida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "El usuario autenticado no es el creador del grupo.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El grupo no existe.", typeof(ProblemDetails))]
    public async Task<IActionResult> Eliminar(int grupoId, CancellationToken cancellationToken = default)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        await grupoServicio.Eliminar(grupoId, usuarioId.Value, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("{grupoId:int}/unirse")]
    [SwaggerOperation(
        Summary = "Une al usuario autenticado a un grupo.",
        Description = "Requiere autenticacion. Agrega directamente al usuario como integrante porque los grupos son publicos. Repetir la operacion cuando ya pertenece al grupo no genera duplicados.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "El usuario pertenece al grupo.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador del grupo no es valido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticacion valida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El grupo o el usuario no existen.", typeof(ProblemDetails))]
    public async Task<IActionResult> Unirse(int grupoId, CancellationToken cancellationToken = default)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        await grupoServicio.Unirse(grupoId, usuarioId.Value, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{grupoId:int}/salir")]
    [SwaggerOperation(
        Summary = "Abandona un grupo.",
        Description = "Requiere autenticacion. Elimina la membresia del usuario autenticado. El creador no puede abandonar su propio grupo; debe eliminarlo.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "El usuario abandono el grupo.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador no es valido o el usuario es el creador.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticacion valida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El grupo no existe o el usuario no es integrante.", typeof(ProblemDetails))]
    public async Task<IActionResult> Salir(int grupoId, CancellationToken cancellationToken = default)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        await grupoServicio.Salir(grupoId, usuarioId.Value, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{grupoId:int}/integrantes/{usuarioIdAEliminar:int}")]
    [SwaggerOperation(
        Summary = "Expulsa a un integrante del grupo.",
        Description = "Requiere autenticacion. Solo el creador puede eliminar integrantes y no puede expulsarse a si mismo. Los recuerdos publicados por la persona expulsada se conservan.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Integrante eliminado del grupo.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Los identificadores no son validos o se intento expulsar al creador.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticacion valida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "El usuario autenticado no es el creador del grupo.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El grupo no existe o el usuario no es integrante.", typeof(ProblemDetails))]
    public async Task<IActionResult> Expulsar(
        int grupoId, int usuarioIdAEliminar, CancellationToken cancellationToken = default)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        await grupoServicio.Expulsar(
            grupoId, usuarioId.Value, usuarioIdAEliminar, cancellationToken);
        return NoContent();
    }

    [HttpGet("{grupoId:int}/integrantes")]
    [SwaggerOperation(
        Summary = "Obtiene los integrantes de un grupo.",
        Description = "Devuelve publicamente los usuarios que integran el grupo, la fecha en que se unieron e indica quien es el creador.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Listado de integrantes.", typeof(List<GrupoIntegranteDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador del grupo no es valido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El grupo no existe.", typeof(ProblemDetails))]
    public async Task<ActionResult<IReadOnlyList<GrupoIntegranteDto>>> ObtenerIntegrantes(
        int grupoId, CancellationToken cancellationToken = default)
    {
        return Ok(await grupoServicio.ObtenerIntegrantes(grupoId, cancellationToken));
    }

    [HttpGet("{grupoId:int}/recuerdos")]
    [SwaggerOperation(
        Summary = "Obtiene los recuerdos de un grupo.",
        Description = "Devuelve publicamente las publicaciones visibles asociadas al grupo, ordenadas desde la mas reciente. Estos recuerdos tambien forman parte de los feeds normales.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Listado de recuerdos visibles.", typeof(List<PublicacionDetalleDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador del grupo no es valido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El grupo no existe.", typeof(ProblemDetails))]
    public async Task<ActionResult<IReadOnlyList<PublicacionDetalleDto>>> ObtenerRecuerdos(
        int grupoId, CancellationToken cancellationToken = default)
    {
        return Ok(await grupoServicio.ObtenerRecuerdos(grupoId, cancellationToken));
    }

    [Authorize]
    [HttpPost("{grupoId:int}/recuerdos")]
    [SwaggerOperation(
        Summary = "Publica un recuerdo dentro de un grupo.",
        Description = "Requiere autenticacion y pertenecer al grupo. Crea una publicacion asociada al grupo usando multipart/form-data y permite enviar un archivo o una URL HTTPS. El recuerdo tambien aparece en los feeds normales.")]
    [SwaggerResponse(StatusCodes.Status201Created, "Recuerdo creado. Devuelve su id y la ubicacion en Location.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador o los datos del recuerdo no son validos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una autenticacion valida.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "El usuario autenticado no pertenece al grupo.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El grupo o el usuario no existen.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "Formato no compatible; utilice multipart/form-data.", typeof(ProblemDetails))]
    public async Task<IActionResult> CrearRecuerdo(
        int grupoId, [FromForm] CrearRecuerdoGrupoRequest request,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        using var stream = request.Archivo?.OpenReadStream();
        var id = await grupoServicio.CrearRecuerdo(
            grupoId,
            usuarioId.Value,
            request.ToPublicacion(grupoId, usuarioId.Value),
            stream,
            request.Archivo?.FileName,
            cancellationToken);
        return Created($"/api/publicaciones/{id}", new { id });
    }
}
