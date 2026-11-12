namespace ProyectoFinalTPI.Backend.Dtos.Usuario;

public sealed record CategoriaCuentaDto(
    string Codigo,
    string Nombre,
    int? PublicacionesSemanales,
    bool PublicacionesIlimitadas,
    bool PermitePrimerRecuerdo,
    string PrioridadVisibilidad,
    string[] Permisos);