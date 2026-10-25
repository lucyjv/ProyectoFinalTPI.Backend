using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ProyectoFinalTPI.Backend.Entidades;

public static class IdentidadMomento
{
    public static string Normalizar(string texto) => string.Join(" ", new string(NormalizadorMomento.Normalizar(texto)
        .Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static string Clave(string nombre, DateOnly? inicio, DateOnly? fin) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{Normalizar(nombre)}|{inicio?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "?"}|{fin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "?"}")));
}
