using System.Security.Cryptography;
using System.Globalization;
using System.Text;

namespace ProyectoFinalTPI.Backend.Entidades;

public static class ReglasMomento
{
    public static string Normalizar(string texto)
    {
        var sinMarcas = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return string.Join(" ", sinMarcas.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Normalize(NormalizationForm.FormC);
    }
    // La identidad descarta puntuación; los alias conservan su normalización histórica.
    public static string NormalizarIdentidad(string texto) => string.Join(" ", new string(Normalizar(texto)
        .Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static string ClaveIdentidad(string nombre, DateOnly? inicio, DateOnly? fin) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{NormalizarIdentidad(nombre)}|{inicio?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "?"}|{fin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "?"}")));
}
