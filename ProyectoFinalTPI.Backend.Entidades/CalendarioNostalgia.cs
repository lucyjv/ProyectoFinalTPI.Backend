namespace ProyectoFinalTPI.Backend.Entidades;

public static class CalendarioNostalgia
{
    private static readonly TimeZoneInfo BuenosAires =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public static DateOnly FechaLocal(DateTimeOffset instante) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, BuenosAires).DateTime);

    public static DateTimeOffset InicioDelDia(DateOnly fecha) =>
        TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(TimeOnly.MinValue), BuenosAires);
}
