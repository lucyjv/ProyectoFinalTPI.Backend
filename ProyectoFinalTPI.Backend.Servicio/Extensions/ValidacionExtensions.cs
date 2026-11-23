namespace ProyectoFinalTPI.Backend.Servicio.Extensions;

public static class ValidacionExtensions
{
    public static void ValidarId(this int id, string recurso)
    {
        if (id <= 0)
            throw new ArgumentException(
                $"El ID de {recurso} debe ser mayor que cero.");
    }
    public static void ValidarLimite(this int limite, int maximo)
    {
        if (limite < 1 || limite > maximo)
            throw new ArgumentException(
                $"El límite debe estar entre 1 y {maximo}.");
    }
    public static string ValidarTexto(this string? texto, int maximo, string campo, int minimo = 1)
    {
        var normalizado = texto?.Trim() ?? string.Empty;

        if (normalizado.Length < minimo || normalizado.Length > maximo)
            throw new ArgumentException(
                $"El campo {campo} debe tener entre {minimo} y {maximo} caracteres.");

        return normalizado;
    }
    public static void ValidarEnum<T>(this T valor, string campo) where T : struct, Enum
    {
        if (!Enum.IsDefined(valor))
            throw new ArgumentException($"El campo {campo} no es válido.");
    }
    public static void ValidarRango(
    this int valor, int minimo, int maximo, string campo)
    {
        if (valor < minimo || valor > maximo)
            throw new ArgumentException(
                $"El campo {campo} debe estar entre {minimo} y {maximo}.");
    }
}