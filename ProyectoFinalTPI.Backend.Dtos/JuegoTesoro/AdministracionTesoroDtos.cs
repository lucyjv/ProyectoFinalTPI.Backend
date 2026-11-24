namespace ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;

public sealed record GuardarUbicacionTesoroDto(string Nombre, string Pista, double Latitud,
    double Longitud, double ZoomMinimo, bool Activa);
public sealed record UbicacionTesoroDto(int Id, string Nombre, string Pista, double Latitud,
    double Longitud, double ZoomMinimo, bool Activa);
public sealed record ProgramarTesoroDto(int? UbicacionId);
public sealed record EditarRetoTesoroDto(string Nombre, string Pista, double Latitud, double Longitud, double ZoomMinimo);
public sealed record RetoTesoroAdminDto(int Id, string Nombre, string Pista, double Latitud, double Longitud, double ZoomMinimo);
public sealed record SemanaTesoroAdminDto(DateOnly SemanaInicio, bool Editable, string Modo,
    int? UbicacionId, string? Nombre, string? Pista);
public sealed record PanelTesoroAdminDto(bool Habilitado, DateOnly SemanaActual, RetoTesoroAdminDto? Actual,
    IReadOnlyList<UbicacionTesoroDto> Ubicaciones, IReadOnlyList<SemanaTesoroAdminDto> Semanas);
