namespace ProyectoFinalTPI.Backend.Dtos.Publicacion;

public class TendenciaHashtagDto
{
    public string Hashtag { get; set; } = string.Empty;
    public int CantidadPublicaciones { get; set; }
    public int CantidadAutores { get; set; }
}
