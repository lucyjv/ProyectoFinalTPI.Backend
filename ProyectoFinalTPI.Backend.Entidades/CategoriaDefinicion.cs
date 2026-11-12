namespace ProyectoFinalTPI.Backend.Entidades.Categorias;

public sealed class CategoriaDefinicion
{
    public CategoriaCuenta Codigo { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public int? PublicacionesSemanales { get; init; }
    public PrioridadVisibilidad Visibilidad { get; init; }

    public IReadOnlyList<PermisoCuenta> Permisos { get; init; } =
        Array.Empty<PermisoCuenta>();

    public bool PublicacionesIlimitadas { get; init; }
    public bool PermitePrimerRecuerdo { get; init; }
}