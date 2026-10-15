using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinalTPI.Backend.Entidades;

public class SesionUsuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaExpiracion { get; set; }
    public DateTime? FechaRevocacion { get; set; }
    public DateTime? UltimoUso { get; set; }
    public string? UserAgent { get; set; }
    public uint Version { get; set; }

    [NotMapped]
    public bool EstaActiva => FechaRevocacion is null && FechaExpiracion > DateTime.UtcNow;
}
