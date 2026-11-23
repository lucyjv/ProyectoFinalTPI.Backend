using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Capsulas;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/capsulas")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CapsulasController(ICapsulaServicio servicio) : ControllerBase
{
    /// <summary>Lista tus cápsulas, incluidas las privadas, por páginas.</summary>
    [Authorize, HttpGet("mias")]
    public Task<ResultadoPaginadoDto<CapsulaDto>> ObtenerMias([FromQuery] string? cursor = null, [FromQuery] int limite = 20,
        CancellationToken cancellationToken = default) =>
        servicio.Listar(User.ObtenerUsuarioIdRequerido(), true, cursor, limite, cancellationToken);

    /// <summary>Lista únicamente las cápsulas públicas de un usuario.</summary>
    [AllowAnonymous, HttpGet]
    public Task<ResultadoPaginadoDto<CapsulaDto>> ObtenerPublicas([FromQuery] int propietarioId,
        [FromQuery] string? cursor = null, [FromQuery] int limite = 20, CancellationToken cancellationToken = default) =>
        servicio.Listar(propietarioId, false, cursor, limite, cancellationToken);

    /// <summary>Busca tus recuerdos originales para agregarlos a una cápsula propia.</summary>
    [Authorize, HttpGet("recuerdos-disponibles")]
    public Task<ResultadoPaginadoDto<RecuerdoDisponibleCapsulaDto>> ObtenerDisponibles([FromQuery] int? capsulaId = null,
        [FromQuery] string? cursor = null, [FromQuery] int limite = 20, CancellationToken cancellationToken = default) =>
        servicio.ObtenerDisponibles(User.ObtenerUsuarioIdRequerido(), capsulaId, cursor, limite, cancellationToken);

    /// <summary>Obtiene los datos de una cápsula pública o de una propia.</summary>
    [AllowAnonymous, HttpGet("{id:int}")]
    public Task<CapsulaDto> Obtener(int id, CancellationToken cancellationToken = default) =>
        servicio.Obtener(id, User.ObtenerUsuarioId(), cancellationToken);

    /// <summary>Crea una cápsula privada por defecto y admite recuerdos propios ya publicados.</summary>
    [Authorize, HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Crear([FromBody] CrearCapsulaDto datos, CancellationToken cancellationToken = default)
    {
        var capsula = await servicio.Crear(User.ObtenerUsuarioIdRequerido(), datos, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = capsula.Id }, capsula);
    }

    /// <summary>Actualiza los datos, la visibilidad y la selección de portada de una cápsula propia.</summary>
    [Authorize, HttpPut("{id:int}")]
    [Consumes("application/json")]
    public Task<CapsulaDto> Actualizar(int id, [FromBody] ActualizarCapsulaDto datos, CancellationToken cancellationToken = default) =>
        servicio.Actualizar(id, User.ObtenerUsuarioIdRequerido(), datos, cancellationToken);

    /// <summary>Crea una cápsula con una portada subida desde el dispositivo.</summary>
    // Misma ruta que JSON, elegida por Content-Type. La documentación del contrato multipart está en docs/CAPSULAS.md.
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize, HttpPost, Consumes("multipart/form-data"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> CrearConFoto([FromForm] CrearCapsulaDto datos, [FromForm] IFormFile archivo, CancellationToken cancellationToken)
    {
        ValidarArchivo(archivo);
        await using var contenido = archivo.OpenReadStream();
        var capsula = await servicio.CrearConPortada(User.ObtenerUsuarioIdRequerido(), datos, contenido, archivo.ContentType, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = capsula.Id }, capsula);
    }

    /// <summary>Actualiza los datos y reemplaza la portada con una foto del dispositivo.</summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize, HttpPut("{id:int}"), Consumes("multipart/form-data"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<CapsulaDto> ActualizarConFoto(int id, [FromForm] ActualizarCapsulaDto datos, [FromForm] IFormFile archivo, CancellationToken cancellationToken)
    {
        ValidarArchivo(archivo);
        await using var contenido = archivo.OpenReadStream();
        return await servicio.ActualizarConPortada(id, User.ObtenerUsuarioIdRequerido(), datos, contenido, archivo.ContentType, cancellationToken);
    }

    /// <summary>Entrega la portada subida solo cuando el visitante puede acceder a la cápsula.</summary>
    [AllowAnonymous, HttpGet("{id:int}/portada")]
    public async Task<IActionResult> ObtenerPortada(int id, CancellationToken cancellationToken)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(await servicio.ObtenerPortada(id, User.ObtenerUsuarioId(), cancellationToken), "image/webp");
    }

    private static void ValidarArchivo(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0) throw new ArgumentException("Seleccioná una foto de portada.");
        if (archivo.Length > 5 * 1024 * 1024) throw new ArgumentException("La portada no puede superar 5 MiB.");
    }

    /// <summary>Elimina una cápsula propia sin borrar sus recuerdos.</summary>
    [Authorize, HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken = default)
    {
        await servicio.Eliminar(id, User.ObtenerUsuarioIdRequerido(), cancellationToken);
        return NoContent();
    }

    /// <summary>Recorre los recuerdos de una cápsula accesible por fecha del recuerdo e ID, en orden ascendente.</summary>
    [AllowAnonymous, HttpGet("{id:int}/recuerdos")]
    public Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerRecuerdos(int id, [FromQuery] string? cursor = null,
        [FromQuery] int limite = 20, CancellationToken cancellationToken = default) =>
        servicio.ObtenerRecuerdos(id, User.ObtenerUsuarioId(), cursor, limite, cancellationToken);

    /// <summary>Agrega hasta 100 recuerdos originales propios; los repetidos no se duplican.</summary>
    [Authorize, HttpPost("{id:int}/recuerdos")]
    public async Task<IActionResult> AgregarRecuerdos(int id, [FromBody] AsociarRecuerdosCapsulaDto datos,
        CancellationToken cancellationToken = default)
    {
        await servicio.AgregarRecuerdos(id, User.ObtenerUsuarioIdRequerido(), datos.PublicacionesIds, cancellationToken);
        return NoContent();
    }

    /// <summary>Quita un recuerdo de la cápsula sin eliminar la publicación.</summary>
    [Authorize, HttpDelete("{id:int}/recuerdos")]
    public async Task<IActionResult> QuitarRecuerdo(int id, [FromQuery] int publicacionId, CancellationToken cancellationToken = default)
    {
        await servicio.QuitarRecuerdo(id, User.ObtenerUsuarioIdRequerido(), publicacionId, cancellationToken);
        return NoContent();
    }

    /// <summary>Obtiene pines paginados y los límites globales de la cápsula, sin filtrar por década.</summary>
    [AllowAnonymous, HttpGet("{id:int}/mapa")]
    public Task<MapaCapsulaDto> ObtenerMapa(int id, [FromQuery] ConsultaMapaCapsulaDto filtro,
        [FromQuery] string? cursor = null, [FromQuery] int limite = 200, CancellationToken cancellationToken = default) =>
        servicio.ObtenerMapa(id, User.ObtenerUsuarioId(), filtro, cursor, limite, cancellationToken);
}
