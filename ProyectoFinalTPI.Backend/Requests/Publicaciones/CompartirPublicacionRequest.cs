using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Publicaciones
{
    public class CompartirPublicacionRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int PublicacionOriginalId { get; set; }

        public string Descripcion { get; set; } = string.Empty;

        // Temporal hasta implementar autenticación
        [Required]
        [Range(1, int.MaxValue)]
        public int AutorId { get; set; }
    }
}

