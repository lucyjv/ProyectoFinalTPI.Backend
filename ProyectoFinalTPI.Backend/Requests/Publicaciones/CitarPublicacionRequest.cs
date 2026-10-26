using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Publicaciones
{
    public class CitarPublicacionRequest
    {
        [Required(ErrorMessage = "La descripción de la cita no puede estar vacía.")]
        [MinLength(1, ErrorMessage = "La descripción de la cita no puede estar vacía.")]
        public string Descripcion { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int? AutorId { get; set; }
    }
}
