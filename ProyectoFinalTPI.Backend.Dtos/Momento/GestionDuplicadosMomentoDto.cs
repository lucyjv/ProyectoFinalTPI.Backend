namespace ProyectoFinalTPI.Backend.Dtos.Momento;

public record MomentoCatalogoDto(int Id, string Nombre, DateOnly? FechaInicio, DateOnly? FechaFin, long VersionCatalogo);
public record CandidatoDuplicadoMomentoDto(MomentoCatalogoDto Momento, double Similitud, string Motivo);
public record VistaFusionMomentoDto(MomentoCatalogoDto Origen, MomentoCatalogoDto Destino,
    int PublicacionesAfectadas, int DecisionesEnConflicto, int HijosATrasladar, bool YaFusionado);
public record SolicitudFusionMomentoDto(int DestinoId, long VersionOrigen, long VersionDestino);
public record ResultadoFusionMomentoDto(int OrigenId, int DestinoId, int PublicacionesAfectadas,
    int DecisionesEnConflicto, long VersionDestino, bool YaFusionado);
