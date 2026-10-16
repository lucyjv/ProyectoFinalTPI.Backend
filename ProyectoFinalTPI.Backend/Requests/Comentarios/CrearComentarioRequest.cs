using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Comentarios;

public class CrearComentarioRequest
{
    [Required(ErrorMessage = "Debe indicar el contenido del comentario.")]
    [StringLength(1000, ErrorMessage = "El comentario no puede superar los 1000 caracteres.")]
    public string Contenido { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "El ID del comentario padre debe ser mayor que cero.")]
    public int? ComentarioPadreId { get; set; }
}
