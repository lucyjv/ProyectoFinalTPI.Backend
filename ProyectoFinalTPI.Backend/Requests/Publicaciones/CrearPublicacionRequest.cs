using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Servicio;

using global::ProyectoFinalTPI.Backend.Entidades;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Publicaciones
{
    public class CrearPublicacionRequest : IValidatableObject
    {
        [Required]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        public string Descripcion { get; set; } = string.Empty;

        [Required]
        public DateTime Fecha { get; set; }

        [Required]
        [EnumDataType(typeof(CategoriaEnum))]
        public CategoriaEnum Categoria { get; set; }

        [Required]
        public string NombreLugar { get; set; } = string.Empty;

        [Range(-90, 90)]
        public double Latitud { get; set; }

        [Range(-180, 180)]
        public double Longitud { get; set; }

        [Range(-90, 90)]
        public double? GpsLatitud { get; set; }

        [Range(-180, 180)]
        public double? GpsLongitud { get; set; }

        [Required]
        [EnumDataType(typeof(MultimediaEnum))]
        public MultimediaEnum TipoMultimedia { get; set; }

        public List<AdjuntoPublicacionRequest> Adjuntos { get; set; } = new();
        public MusicaPublicacionDto? Musica { get; set; }
        public IFormFile? Archivo { get; set; }

        public string? UrlArchivo { get; set; }

        // Temporal hasta implementar autenticación
        [Range(1, int.MaxValue)]
        public int AutorId { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (GpsLatitud.HasValue != GpsLongitud.HasValue)
                yield return new ValidationResult("La ubicación actual requiere latitud y longitud.", new[] { nameof(GpsLatitud), nameof(GpsLongitud) });
            if (Adjuntos.Count > 4)
                yield return new ValidationResult("Una publicación admite hasta 4 adjuntos.", new[] { nameof(Adjuntos) });
            if (Adjuntos.Count > 0 && (Archivo != null || !string.IsNullOrWhiteSpace(UrlArchivo)))
                yield return new ValidationResult("No combines Adjuntos con Archivo o UrlArchivo.", new[] { nameof(Adjuntos) });
            if (Fecha == default)
                yield return new ValidationResult("Debe indicar una fecha.", new[] { nameof(Fecha) });

            if (Archivo != null && Archivo.Length == 0)
                yield return new ValidationResult("El archivo no puede estar vacío.", new[] { nameof(Archivo) });

            var requerirMultimedia = validationContext.GetService(typeof(IOptions<OpcionesPublicacion>))
                is IOptions<OpcionesPublicacion> opciones && opciones.Value.RequerirMultimedia;

            if (requerirMultimedia && Adjuntos.Count == 0 && Archivo == null && string.IsNullOrWhiteSpace(UrlArchivo))
            {
                yield return new ValidationResult(
                    "Debe subir un archivo o indicar una URL.",
                    new[] { nameof(Archivo), nameof(UrlArchivo) });
            }

            if (Archivo != null && !string.IsNullOrWhiteSpace(UrlArchivo))
            {
                yield return new ValidationResult(
                    "Debe enviar un archivo o una URL, no ambos.",
                    new[] { nameof(Archivo), nameof(UrlArchivo) });
            }

            if (Archivo != null && Archivo.Length > 50 * 1024 * 1024)
            {
                yield return new ValidationResult(
                    "El archivo no puede superar los 50 MB.",
                    new[] { nameof(Archivo) });
            }

            if (!string.IsNullOrWhiteSpace(UrlArchivo) &&
                (!Uri.TryCreate(UrlArchivo, UriKind.Absolute, out var uri) ||
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                yield return new ValidationResult(
                    "La URL debe ser HTTPS.",
                    new[] { nameof(UrlArchivo) });
            }
        }


    }
}
