namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public class ErrorProveedorAnalisis(string codigo, bool permanente = false, TimeSpan? reintentarEn = null)
    : Exception("El proveedor de análisis no pudo completar la solicitud.")
{
    public string Codigo { get; } = codigo;
    public bool Permanente { get; } = permanente;
    public TimeSpan? ReintentarEn { get; } = reintentarEn;
}

