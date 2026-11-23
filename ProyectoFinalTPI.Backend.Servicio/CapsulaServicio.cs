using System.Globalization;
using Microsoft.Extensions.Logging;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Capsulas;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class CapsulaServicio(ICapsulaRepositorio capsulas, IUsuarioRepositorio usuarios,
    IPublicacionServicio publicaciones, IEtiquetadoPublicacionServicio etiquetas, TimeProvider reloj,
    IPortadaCapsulaAlmacenamiento? portadas = null, ILogger<CapsulaServicio>? logger = null) : ICapsulaServicio
{
    public async Task<ResultadoPaginadoDto<CapsulaDto>> Listar(int propietarioId, bool propias, string? cursor, int limite, CancellationToken ct)
    {
        await ExigirUsuario(propietarioId);
        ValidarLimite(limite);
        var ambito = propias ? "mias" : "publicas";
        var avance = CursorCapsulas.Leer(cursor, ambito, propietarioId);
        var encontrados = await capsulas.Listar(propietarioId, propias, avance?.Id, limite + 1, ct);
        return Paginar(encontrados, limite, c => CursorCapsulas.Crear(ambito, propietarioId, c.Id));
    }

    public async Task<CapsulaDto> Obtener(int id, int? lectorId, CancellationToken ct)
    {
        ValidarId(id);
        return await capsulas.Obtener(id, lectorId, ct) ?? throw NoDisponible();
    }

    public async Task<CapsulaDto> Crear(int propietarioId, CrearCapsulaDto datos, CancellationToken ct)
        => await Obtener(await GuardarNueva(propietarioId, datos, null, ct), propietarioId, ct);

    private async Task<int> GuardarNueva(int propietarioId, CrearCapsulaDto datos, string? recursoId, CancellationToken ct)
    {
        await ExigirUsuario(propietarioId);
        NormalizarDatos(datos);
        var ids = ReglasCapsulas.NormalizarIds(datos.PublicacionesIds);
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var capsula = new Capsula
        {
            PropietarioId = propietarioId, Nombre = datos.Nombre, Descripcion = datos.Descripcion,
            Visibilidad = datos.Visibilidad, PortadaPublicacionId = datos.PortadaPublicacionId,
            PortadaAdjuntoId = datos.PortadaAdjuntoId, FechaCreacion = ahora, FechaActualizacion = ahora
        };
        capsula.PortadaRecursoId = recursoId;
        return await capsulas.Crear(capsula, ids, ct);
    }

    public async Task<CapsulaDto> Actualizar(int id, int propietarioId, ActualizarCapsulaDto datos, CancellationToken ct)
    {
        await ExigirPropietario(id, propietarioId, ct);
        NormalizarDatos(datos);
        var anterior = await capsulas.Actualizar(id, propietarioId, datos, ct);
        await LimpiarPortada(anterior);
        return await Obtener(id, propietarioId, ct);
    }

    public async Task Eliminar(int id, int propietarioId, CancellationToken ct)
    {
        await ExigirPropietario(id, propietarioId, ct);
        var anterior = await capsulas.Eliminar(id, propietarioId, ct);
        await LimpiarPortada(anterior);
    }

    public async Task<CapsulaDto> CrearConPortada(int propietarioId, CrearCapsulaDto datos, Stream archivo, string contentType, CancellationToken ct)
    {
        await ExigirUsuario(propietarioId);
        NormalizarDatos(datos);
        ReglasCapsulas.NormalizarIds(datos.PublicacionesIds);
        ExigirSoloArchivo(datos);
        var resultado = await ConArchivo(archivo, contentType, async recurso =>
            new CapsulaDto { Id = await GuardarNueva(propietarioId, datos, recurso, ct) }, ct);
        return await Obtener(resultado.Id, propietarioId, ct);
    }

    public async Task<CapsulaDto> ActualizarConPortada(int id, int propietarioId, ActualizarCapsulaDto datos, Stream archivo, string contentType, CancellationToken ct)
    {
        await ExigirPropietario(id, propietarioId, ct);
        NormalizarDatos(datos);
        ExigirSoloArchivo(datos);
        string? anterior = null;
        var resultado = await ConArchivo(archivo, contentType, async recurso =>
        {
            anterior = await capsulas.ActualizarConPortada(id, propietarioId, datos, recurso, ct);
            // No incluimos una consulta cancelable posterior en el bloque de compensación:
            // una vez confirmado el cambio, la imagen nueva ya pertenece a la cápsula.
            return new CapsulaDto { Id = id };
        }, ct);
        await LimpiarPortada(anterior);
        return await Obtener(resultado.Id, propietarioId, ct);
    }

    public async Task<byte[]> ObtenerPortada(int id, int? lectorId, CancellationToken ct)
    {
        ValidarId(id);
        var recurso = await capsulas.ObtenerRecursoPortada(id, lectorId, ct) ?? throw NoDisponible();
        return await Almacenamiento.Leer(recurso, ct);
    }

    private IPortadaCapsulaAlmacenamiento Almacenamiento => portadas ?? throw new InvalidOperationException("El almacenamiento de portadas no está configurado.");

    private async Task<CapsulaDto> ConArchivo(Stream archivo, string contentType, Func<string, Task<CapsulaDto>> guardar, CancellationToken ct)
    {
        var bytes = await FotoPortadaCapsula.LeerYValidar(archivo, contentType, ct);
        using var contenido = new MemoryStream(bytes);
        var recurso = await Almacenamiento.Subir(contenido, ct);
        try { return await guardar(recurso); }
        catch { await LimpiarPortada(recurso); throw; }
    }

    private async Task LimpiarPortada(string? recurso)
    {
        if (recurso is null) return;
        try { await Almacenamiento.Eliminar(recurso, CancellationToken.None); }
        catch { logger?.LogWarning("Quedó pendiente limpiar una portada de cápsula: {RecursoId}", recurso); }
    }

    private static void ExigirSoloArchivo(ActualizarCapsulaDto datos)
    {
        if (datos.PortadaPublicacionId.HasValue || datos.PortadaAdjuntoId.HasValue)
            throw new ArgumentException("Elegí una foto de un recuerdo o un archivo, no ambos.");
    }

    public async Task AgregarRecuerdos(int id, int propietarioId, IReadOnlyCollection<int> publicacionesIds, CancellationToken ct)
    {
        await ExigirPropietario(id, propietarioId, ct);
        await capsulas.AgregarRecuerdos(id, propietarioId, ReglasCapsulas.NormalizarIds(publicacionesIds, false), ct);
    }

    public async Task QuitarRecuerdo(int id, int propietarioId, int publicacionId, CancellationToken ct)
    {
        await ExigirPropietario(id, propietarioId, ct);
        ValidarId(publicacionId);
        await capsulas.QuitarRecuerdo(id, propietarioId, publicacionId, ct);
    }

    public async Task<ResultadoPaginadoDto<RecuerdoDisponibleCapsulaDto>> ObtenerDisponibles(int propietarioId, int? capsulaId,
        string? cursor, int limite, CancellationToken ct)
    {
        await ExigirUsuario(propietarioId);
        ValidarLimite(limite);
        if (capsulaId.HasValue) await ExigirPropietario(capsulaId.Value, propietarioId, ct);
        var ambito = $"disponibles:{capsulaId.GetValueOrDefault()}";
        var avance = CursorCapsulas.Leer(cursor, ambito, propietarioId, true);
        var encontrados = await capsulas.ObtenerDisponibles(propietarioId, capsulaId, avance?.Fecha, avance?.Id, limite + 1, ct);
        return Paginar(encontrados, limite, r => CursorCapsulas.Crear(ambito, propietarioId, r.Id, r.Fecha));
    }

    public async Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerRecuerdos(int id, int? lectorId,
        string? cursor, int limite, CancellationToken ct)
    {
        await Obtener(id, lectorId, ct);
        ValidarLimite(limite);
        var avance = CursorCapsulas.Leer(cursor, "recuerdos", id, true);
        var referencias = await capsulas.ObtenerReferencias(id, lectorId, avance?.Fecha, avance?.Id, limite + 1, ct);
        var pagina = Paginar(referencias, limite, r => CursorCapsulas.Crear("recuerdos", id, r.Id, r.Fecha));
        var detalles = await publicaciones.ObtenerDetallesPorIds(pagina.Items.Select(r => r.Id).ToArray(), lectorId, ct);
        await etiquetas.CompletarEtiquetas(detalles, ct);
        return new() { Items = detalles, SiguienteCursor = pagina.SiguienteCursor };
    }

    public async Task<MapaCapsulaDto> ObtenerMapa(int id, int? lectorId, ConsultaMapaCapsulaDto filtro,
        string? cursor, int limite, CancellationToken ct)
    {
        await Obtener(id, lectorId, ct);
        ValidarLimite(limite, 200);
        ArgumentNullException.ThrowIfNull(filtro);
        ValidarArea(filtro);
        var ambito = string.Create(CultureInfo.InvariantCulture,
            $"mapa:{filtro.MinLatitud}:{filtro.MaxLatitud}:{filtro.MinLongitud}:{filtro.MaxLongitud}");
        var avance = CursorCapsulas.Leer(cursor, ambito, id);
        var pines = await capsulas.ObtenerPines(id, lectorId, filtro, avance?.Id, limite + 1, ct);
        var pagina = Paginar(pines, limite, p => CursorCapsulas.Crear(ambito, id, p.Id));
        return new MapaCapsulaDto
        {
            Items = pagina.Items, SiguienteCursor = pagina.SiguienteCursor,
            Limites = await capsulas.ObtenerLimites(id, lectorId, ct)
        };
    }

    private async Task ExigirUsuario(int id)
    {
        ValidarId(id);
        var usuario = await usuarios.ObtenerPorIdAsync(id);
        if (usuario is null || usuario.EstaEliminado) throw new KeyNotFoundException("El usuario no existe o no está disponible.");
    }

    private async Task ExigirPropietario(int id, int propietarioId, CancellationToken ct)
    {
        await ExigirUsuario(propietarioId);
        var capsula = await Obtener(id, propietarioId, ct);
        if (capsula.PropietarioId != propietarioId) throw NoDisponible();
    }

    private static void NormalizarDatos(ActualizarCapsulaDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        datos.Nombre = datos.Nombre?.Trim() ?? string.Empty;
        datos.Descripcion = string.IsNullOrWhiteSpace(datos.Descripcion) ? null : datos.Descripcion.Trim();
        if (datos.Nombre.Length is < 3 or > 100 || datos.Descripcion?.Length > 500)
            throw new ArgumentException("El nombre debe tener entre 3 y 100 caracteres y la descripción hasta 500.");
        if (!Enum.IsDefined(datos.Visibilidad)) throw new ArgumentException("La visibilidad no es válida.");
        if (datos.PortadaPublicacionId is <= 0 || datos.PortadaAdjuntoId is <= 0 ||
            datos.PortadaAdjuntoId.HasValue && !datos.PortadaPublicacionId.HasValue)
            throw new ArgumentException("La selección de portada no es válida.");
    }

    private static void ValidarId(int id)
    {
        if (id <= 0) throw new ArgumentException("El identificador debe ser positivo.");
    }

    private static void ValidarLimite(int limite, int maximo = 50)
    {
        if (limite < 1 || limite > maximo) throw new ArgumentException($"El límite debe estar entre 1 y {maximo}.");
    }

    private static void ValidarArea(ConsultaMapaCapsulaDto filtro)
    {
        var valores = new[] { filtro.MinLatitud, filtro.MaxLatitud, filtro.MinLongitud, filtro.MaxLongitud };
        if (valores.All(v => !v.HasValue)) return;
        if (valores.Any(v => !v.HasValue || !double.IsFinite(v.Value)) ||
            filtro.MinLatitud < -90 || filtro.MaxLatitud > 90 || filtro.MinLatitud >= filtro.MaxLatitud ||
            filtro.MinLongitud < -180 || filtro.MaxLongitud > 180 || filtro.MinLongitud >= filtro.MaxLongitud)
            throw new ArgumentException("Indicá un área completa y válida para el mapa.");
    }

    private static ResultadoPaginadoDto<T> Paginar<T>(List<T> encontrados, int limite, Func<T, string> cursor)
    {
        var items = encontrados.Take(limite).ToList();
        return new() { Items = items, SiguienteCursor = encontrados.Count > limite ? cursor(items[^1]) : null };
    }

    private static KeyNotFoundException NoDisponible() => new("La cápsula no existe o no está disponible.");
}
