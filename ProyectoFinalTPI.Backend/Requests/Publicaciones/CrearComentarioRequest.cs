using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Publicaciones
{
    public class CrearComentarioRequest
    {
        [Required(ErrorMessage = "Debe indicar el contenido del comentario.")]
        public string Contenido { get; set; } = string.Empty;

        // Temporal, igual que AutorId en creación, hasta integrar el usuario del token.
        [Range(1, int.MaxValue, ErrorMessage = "El ID del usuario debe ser mayor que cero.")]
        public int UsuarioId { get; set; }
    }
}
