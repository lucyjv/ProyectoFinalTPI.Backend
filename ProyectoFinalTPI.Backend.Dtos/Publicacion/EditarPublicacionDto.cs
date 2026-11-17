using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion
{
    public class EditarPublicacionDto
    {
        [Required(ErrorMessage = "El título es obligatorio.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "El título debe tener entre 1 y 200 caracteres.")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(5000, MinimumLength = 1, ErrorMessage = "La descripción debe tener entre 1 y 5000 caracteres.")]
        public string Descripcion { get; set; } = string.Empty;
    }
}
