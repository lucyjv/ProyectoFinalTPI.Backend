using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Servicio;

internal static class CalendarioTesoro
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
    public static DateOnly Semana(TimeProvider reloj)
    {
        var fecha = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), Zona).DateTime);
        return fecha.AddDays(-(int)fecha.DayOfWeek);
    }
    public static DateTime InicioUtc(DateOnly fecha) => TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(TimeOnly.MinValue), Zona);
    public static UbicacionTesoro? Elegir(DateOnly semana, DateOnly semanaBase,
        IReadOnlyList<UbicacionTesoro> ubicaciones, ProgramacionTesoro? programacion)
    {
        if (programacion is not null)
            return ubicaciones.FirstOrDefault(u => u.Id == programacion.UbicacionId && u.Activa);
        var activas = ubicaciones.Where(u => u.Activa).OrderBy(u => u.Id).ToList();
        if (activas.Count == 0) return null;
        var semanas = (int)Math.Floor((semana.DayNumber - semanaBase.DayNumber) / 7d);
        return activas[((semanas % activas.Count) + activas.Count) % activas.Count];
    }
}
