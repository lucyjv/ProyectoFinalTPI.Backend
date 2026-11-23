namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class CompraPuntosNostalgia
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string PackId { get; set; } = string.Empty;
    public int Puntos { get; set; }
    public decimal PrecioUsd { get; set; }
    public decimal CotizacionDolar { get; set; }
    public decimal PrecioArs { get; set; }
    public string Estado { get; set; } = "Pendiente"; // "Pendiente", "Aprobado", "Rechazado"
    public string? PreferenceId { get; set; }
    public string? PaymentId { get; set; }
    public DateTimeOffset FechaCreacion { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FechaAcreditacion { get; set; }
}
