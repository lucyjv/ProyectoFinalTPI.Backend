using System.Globalization;
using System.Text;
using System.Text.Json;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class ExploracionMomentosServicio(IExploracionMomentosRepositorio repositorio, IRelacionRecuerdosRepositorio relaciones) : IExploracionMomentosServicio
{
    public async Task<ResultadoPaginadoDto<MomentoResumenDto>> Buscar(string? busqueda, string? cursor, int limite, CancellationToken ct)
    {
        ValidarLimite(limite);
        var texto = (busqueda ?? "").Trim().ToLowerInvariant();
        if (texto.Length > 200) throw new ArgumentException("La búsqueda admite hasta 200 caracteres.");
        var normalizado = string.Join(" ", new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (texto != "" && normalizado == "") throw new ArgumentException("La búsqueda no es válida.");
        var posicion = LeerCursor(cursor, "buscar", 0, texto, false);
        var items = await repositorio.Buscar(texto, normalizado, posicion?.Id, limite + 1, ct);
        return Paginar(items, limite, ultimo => new("buscar", 0, texto, false, null, ultimo.Id));
    }

    public async Task<MomentoDetalleDto> ObtenerDetalle(int id, CancellationToken ct)
    {
        ValidarId(id);
        return await repositorio.ObtenerDetalle(id, ct) ?? throw new KeyNotFoundException("El momento no existe.");
    }

    public async Task<ResultadoPaginadoDto<PublicacionResumenDto>> ObtenerPublicaciones(int id, bool descendientes, string? cursor, int limite, CancellationToken ct)
    {
        ValidarLimite(limite);
        await ObtenerDetalle(id, ct);
        var posicion = LeerCursor(cursor, "publicaciones", id, "", descendientes);
        var items = await repositorio.ObtenerPublicaciones(id, descendientes, posicion, limite + 1, ct);
        return Paginar(items, limite, ultimo => new("publicaciones", id, "", descendientes, ultimo.Fecha, ultimo.Id));
    }

    public async Task<MomentosPublicacionDto> ObtenerEstado(int id, int? usuarioId, CancellationToken ct)
    {
        ValidarId(id);
        var original = await relaciones.ObtenerOriginalVisible(id, ct)
            ?? throw new KeyNotFoundException("La publicación no existe o no está disponible.");
        return await repositorio.ObtenerEstado(original, usuarioId, ct)
            ?? throw new KeyNotFoundException("La publicación no existe o no está disponible.");
    }

    public Task<MomentosPublicacionDto> Corregir(int id, int usuarioId, IReadOnlyCollection<int> momentoIds, bool permitirAutomaticos, long version, CancellationToken ct)
    {
        ValidarId(id); ValidarId(usuarioId);
        if (momentoIds == null || momentoIds.Count > 50 || momentoIds.Any(i => i <= 0) || momentoIds.Distinct().Count() != momentoIds.Count)
            throw new ArgumentException("Seleccioná hasta 50 momentos con identificadores positivos y sin repetir.");
        if (version < 0) throw new ArgumentException("La versión de relaciones no es válida.");
        return repositorio.Corregir(id, usuarioId, momentoIds, permitirAutomaticos, version, ct);
    }

    private static ResultadoPaginadoDto<T> Paginar<T>(List<T> items, int limite, Func<T, CursorExploracionMomentoDto> crearCursor)
    {
        var hayMas = items.Count > limite;
        if (hayMas) items.RemoveAt(items.Count - 1);
        return new()
        {
            Items = items,
            SiguienteCursor = hayMas ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(crearCursor(items[^1]))) : null
        };
    }

    private static CursorExploracionMomentoDto? LeerCursor(string? valor, string tipo, int ambito, string busqueda, bool descendientes)
    {
        if (valor == null) return null;
        try
        {
            if (valor.Length == 0 || valor.Length > 2048) throw new FormatException();
            var posicion = JsonSerializer.Deserialize<CursorExploracionMomentoDto>(Convert.FromBase64String(valor));
            if (posicion == null || posicion.Tipo != tipo || posicion.Ambito != ambito || posicion.Busqueda != busqueda ||
                posicion.Descendientes != descendientes || posicion.Id <= 0 ||
                (tipo == "buscar" && posicion.Fecha != null) ||
                (tipo == "publicaciones" && (posicion.Fecha == null || posicion.Fecha == default(DateTime) || posicion.Fecha.Value.Kind != DateTimeKind.Utc)))
                throw new FormatException();
            return posicion;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new ArgumentException("El cursor no es válido para esta consulta de momentos.");
        }
    }

    private static void ValidarId(int id)
    {
        if (id <= 0) throw new ArgumentException("El identificador debe ser positivo.");
    }
    private static void ValidarLimite(int limite)
    {
        if (limite < 1 || limite > 50) throw new ArgumentException("El límite debe estar entre 1 y 50.");
    }
}
