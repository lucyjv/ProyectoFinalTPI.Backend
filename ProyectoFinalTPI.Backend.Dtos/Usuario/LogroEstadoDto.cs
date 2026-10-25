namespace ProyectoFinalTPI.Backend.Dtos.Usuario;

public class LogroEstadoDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string UrlIcono { get; set; } = string.Empty;
    public int PuntosRecompensa { get; set; }
    public bool Desbloqueado { get; set; }
    public DateTime? FechaDesbloqueo { get; set; }
}
