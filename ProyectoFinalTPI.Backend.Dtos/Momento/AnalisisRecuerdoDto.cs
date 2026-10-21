using System.Text.Json.Serialization;

namespace ProyectoFinalTPI.Backend.Dtos.Momento;

public class ReservaAnalisisDto
{
    public long Id { get; set; }
    public int PublicacionId { get; set; }
    public long VersionContenido { get; set; }
    public int Intentos { get; set; }
    public Guid TokenReserva { get; set; }
    public DateTime ReservadoHasta { get; set; }
}

public record EntradaAnalisisRecuerdoDto(int PublicacionId, long VersionContenido, string Titulo, string Descripcion, DateTime Fecha);
public record MomentoDetectadoDto(int MomentoId, double Confianza);
public record PropuestaMomentoDto([property: JsonRequired] int MomentoId, [property: JsonRequired] string Nombre,
    [property: JsonRequired] int Anio, [property: JsonRequired] string[] Alias,
    [property: JsonRequired] double Confianza, [property: JsonRequired] string Evidencia);
public record CandidatoMomentoDto(int Id, string Nombre, DateOnly? FechaInicio, DateOnly? FechaFin, string[] Alias);
public record ResultadoAnalisisRecuerdoDto(IReadOnlyList<MomentoDetectadoDto> Momentos, bool AnalisisRealizado = true,
    IReadOnlyList<PropuestaMomentoDto>? Propuestas = null);

public class EstadoAnalisisRecuerdoDto
{
    public int PublicacionId { get; set; }
    public long VersionContenido { get; set; }
    public string Estado { get; set; } = "noProgramado";
}
