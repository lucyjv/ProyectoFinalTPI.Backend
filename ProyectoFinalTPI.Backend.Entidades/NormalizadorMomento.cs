using System.Globalization;
using System.Text;

namespace ProyectoFinalTPI.Backend.Entidades;

public static class NormalizadorMomento
{
    public static string Normalizar(string texto)
    {
        var sinMarcas = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return string.Join(" ", sinMarcas.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Normalize(NormalizationForm.FormC);
    }
}
