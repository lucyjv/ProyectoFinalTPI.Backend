using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Publicaciones;

public class ReaccionComentarioRequest
{
    [Required]
    public string Tipo { get; set; } = string.Empty;
}
