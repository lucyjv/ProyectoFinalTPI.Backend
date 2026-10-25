namespace ProyectoFinalTPI.Backend.Dtos.Momento;

public record RecuerdoAReprocesarDto(int PublicacionId, long VersionContenido);
public record SolicitudReprocesamientoDto(IReadOnlyList<RecuerdoAReprocesarDto> Recuerdos,
    bool IncluirFallidos = false, int IntervaloSegundos = 10);
public record CandidatoReprocesamientoDto(int PublicacionId, long VersionContenido, string Titulo, string Estado);
public record VistaPreviaReprocesamientoDto(IReadOnlyList<CandidatoReprocesamientoDto> Recuerdos,
    bool HayMas, int? SiguienteDespuesDeId, bool AnalisisDisponible = false);
public record ItemReprocesamientoDto(int PublicacionId, long VersionContenido, string Resultado,
    string? Motivo = null, DateTime? ProximoIntento = null);
public record ResultadoReprocesamientoDto(IReadOnlyList<ItemReprocesamientoDto> Recuerdos)
{
    public int Programados => Recuerdos.Count(r => r.Resultado != "omitido");
    public int Omitidos => Recuerdos.Count(r => r.Resultado == "omitido");
}
