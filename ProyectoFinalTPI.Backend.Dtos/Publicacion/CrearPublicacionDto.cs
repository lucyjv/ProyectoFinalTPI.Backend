using System.ComponentModel.DataAnnotations;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion
{
    public class CrearPublicacionDto
    {
        [Required(ErrorMessage = "El título es obligatorio.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "El título debe tener entre 3 y 120 caracteres.")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 2000 caracteres.")]
        public string Descripcion { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha es obligatoria.")]
        // Año mínimo: 1909 (persona más vieja registrada). Máximo: año actual, validado en el Value Object Año.
        public DateTime Fecha { get; set; }

        [EnumDataType(typeof(CategoriaEnum), ErrorMessage = "La categoría indicada no es válida.")]
        public CategoriaEnum Categoria { get; set; }

        [Required(ErrorMessage = "El nombre del lugar es obligatorio.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "El nombre del lugar debe tener entre 2 y 200 caracteres.")]
        public string NombreLugar { get; set; } = string.Empty;

        [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
        public double Latitud { get; set; }

        [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
        public double Longitud { get; set; }

        [EnumDataType(typeof(MultimediaEnum), ErrorMessage = "El tipo de multimedia indicado no es válido.")]
        public MultimediaEnum TipoMultimedia { get; set; }

        // Archivo y UrlArchivo son mutuamente excluyentes: exactamente uno debe estar presente.
        // Esa regla se valida en el servicio porque Data Annotations no maneja dependencias entre campos.
        public Stream? Archivo { get; set; }
        public string? NombreArchivo { get; set; }

        [Url(ErrorMessage = "La URL del archivo debe ser una URL válida (https://...).")]
        public string? UrlArchivo { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "El autor debe ser un usuario válido.")]
        public int AutorId { get; set; }
    }
}