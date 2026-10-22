namespace ProyectoFinalTPI.Backend.Dtos.Momento;

public static class ValidadorPropuestasMomento
{
    public static void Validar(IReadOnlyList<PropuestaMomentoDto>? propuestas)
    {
        if (propuestas == null) return;
        if (propuestas.Count > 3 || propuestas.Any(p => p == null || p.MomentoId < 0 ||
            string.IsNullOrWhiteSpace(p.Nombre) || p.Nombre.Length > 200 || p.Anio < 1 || p.Anio > 9999 ||
            (p.MomentoId == 0 && !p.Nombre.Contains(p.Anio.ToString(System.Globalization.CultureInfo.InvariantCulture))) ||
            p.Alias == null || p.Alias.Length > 5 || p.Alias.Any(a => string.IsNullOrWhiteSpace(a) || a.Length > 200) ||
            string.IsNullOrWhiteSpace(p.Evidencia) || p.Evidencia.Length > 500 ||
            !double.IsFinite(p.Confianza) || p.Confianza < 0 || p.Confianza > 1))
            throw new ArgumentException("Las propuestas de momentos no son válidas.");
    }
}
