namespace ProyectoFinalTPI.Backend.Dtos.Auth;

public class ResultadoAutenticacionDto
{
    public AuthRespuestaDto Respuesta { get; set; } = new();
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpira { get; set; }
}
