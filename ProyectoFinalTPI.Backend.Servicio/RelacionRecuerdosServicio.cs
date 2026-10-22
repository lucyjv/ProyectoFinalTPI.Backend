using System.Text.Json;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class RelacionRecuerdosServicio(IRelacionRecuerdosRepositorio repositorio) : IRelacionRecuerdosServicio
{
    public async Task<RelacionesRecuerdoDto> ObtenerRelaciones(int id, CancellationToken ct)
    {
        var original = await ValidarOrigen(id, ct);
        // Consultas secuenciales porque comparten el mismo DbContext.
        return new RelacionesRecuerdoDto
        {
            PublicacionId = original,
            Dia = await Paginar(original, "dia", null, 6, ct),
            Lugar = await Paginar(original, "lugar", null, 6, ct)
        };
    }

    public async Task<ResultadoPaginadoDto<RecuerdoRelacionadoDto>> ObtenerRelacionadas(int id, string tipo, string? cursor, int limite, CancellationToken ct)
    {
        if (tipo != "dia" && tipo != "lugar") throw new ArgumentException("El tipo debe ser dia o lugar.");
        if (limite < 1 || limite > 50) throw new ArgumentException("El límite debe estar entre 1 y 50.");
        var original = await ValidarOrigen(id, ct);
        return await Paginar(original, tipo, LeerCursor(cursor, original, tipo), limite, ct);
    }

    private async Task<int> ValidarOrigen(int id, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentException("El identificador debe ser positivo.");
        return await repositorio.ObtenerOriginalVisible(id, ct)
            ?? throw new KeyNotFoundException("La publicación no existe o no está disponible.");
    }

    private async Task<ResultadoPaginadoDto<RecuerdoRelacionadoDto>> Paginar(int id, string tipo, CursorRelacionDto? cursor, int limite, CancellationToken ct)
    {
        var items = await repositorio.Obtener(id, tipo, cursor, limite + 1, ct);
        var hayMas = items.Count > limite;
        if (hayMas) items.RemoveAt(items.Count - 1);
        return new ResultadoPaginadoDto<RecuerdoRelacionadoDto>
        {
            Items = items,
            SiguienteCursor = hayMas
                ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new CursorRelacionDto(id, tipo, items[^1].DistanciaMetros, items[^1].Id)))
                : null
        };
    }

    private static CursorRelacionDto? LeerCursor(string? valor, int id, string tipo)
    {
        if (valor == null) return null;
        try
        {
            if (valor.Length > 512) throw new FormatException();
            var cursor = JsonSerializer.Deserialize<CursorRelacionDto>(Convert.FromBase64String(valor));
            if (cursor == null || cursor.PublicacionId != id || cursor.Tipo != tipo || cursor.Id <= 0 ||
                !double.IsFinite(cursor.DistanciaMetros) || cursor.DistanciaMetros < 0 || cursor.DistanciaMetros > 200)
                throw new FormatException();
            return cursor;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new ArgumentException("El cursor no es válido para esta publicación y relación.");
        }
    }
}
