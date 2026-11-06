namespace ProyectoFinalTPI.Backend.Dtos.Auth;

public class UsuarioAutenticadoDto
{
    public int UsuarioId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FotoPerfilUrl { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EsAdmin { get; set; }
}
