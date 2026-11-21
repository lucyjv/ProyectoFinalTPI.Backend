using System.Text.Json;

namespace ProyectoFinalTPI.Backend.Servicio;

internal sealed record AvanceCapsulas(string Ambito, int RecursoId, int Id, DateTime? Fecha);

internal static class CursorCapsulas
{
    internal static string Crear(string ambito, int recursoId, int id, DateTime? fecha = null) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new AvanceCapsulas(ambito, recursoId, id, fecha)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static AvanceCapsulas? Leer(string? cursor, string ambito, int recursoId, bool requiereFecha = false)
    {
        if (cursor is null) return null;
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 1024) throw Invalido();
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var avance = JsonSerializer.Deserialize<AvanceCapsulas>(Convert.FromBase64String(base64));
            if (avance is null || avance.Ambito != ambito || avance.RecursoId != recursoId || avance.Id <= 0 ||
                requiereFecha && (!avance.Fecha.HasValue || avance.Fecha == default(DateTime) || avance.Fecha.Value.Kind != DateTimeKind.Utc))
                throw Invalido();
            return avance;
        }
        catch (Exception ex) when (ex is FormatException or JsonException) { throw Invalido(); }
    }

    private static ArgumentException Invalido() => new("El cursor de cápsulas no es válido para esta consulta.");
}

internal static class ReglasCapsulas
{
    internal static int[] NormalizarIds(IReadOnlyCollection<int>? ids, bool permiteVacio = true)
    {
        if (ids is null || ids.Count > 100 || ids.Any(id => id <= 0) || !permiteVacio && ids.Count == 0)
            throw new ArgumentException("Enviá hasta 100 identificadores positivos.");
        return ids.Distinct().ToArray();
    }
}
