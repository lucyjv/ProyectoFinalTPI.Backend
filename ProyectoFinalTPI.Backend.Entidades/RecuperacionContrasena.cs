namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class RecuperacionContrasena
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string EmailDestino { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaExpiracion { get; set; }
    public DateTime? FechaUso { get; set; }
    public DateTime? FechaRevocacion { get; set; }
}
