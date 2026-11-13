using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Servicio;
using ProyectoFinalTPI.Backend.Entidades;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Grupos;

public class CrearRecuerdoGrupoRequest : IValidatableObject
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

    [Required]
    [EnumDataType(typeof(MultimediaEnum))]
    public MultimediaEnum TipoMultimedia { get; set; }

    public List<AdjuntoPublicacionRequest> Adjuntos { get; set; } = new();
    public MusicaPublicacionDto? Musica { get; set; }
    public IFormFile? Archivo { get; set; }
    public string? UrlArchivo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Adjuntos.Count > 4)
            yield return new ValidationResult("Una publicación admite hasta 4 adjuntos.", new[] { nameof(Adjuntos) });
        if (Adjuntos.Count > 0 && (Archivo != null || !string.IsNullOrWhiteSpace(UrlArchivo)))
            yield return new ValidationResult("No combines Adjuntos con Archivo o UrlArchivo.", new[] { nameof(Adjuntos) });
        if (Fecha == default)
            yield return new ValidationResult("Debe indicar una fecha.", [nameof(Fecha)]);

        if (Archivo != null && Archivo.Length == 0)
            yield return new ValidationResult("El archivo no puede estar vacio.", [nameof(Archivo)]);

        var requerirMultimedia = validationContext.GetService(typeof(IOptions<OpcionesPublicacion>))
            is IOptions<OpcionesPublicacion> opciones && opciones.Value.RequerirMultimedia;

        if (requerirMultimedia && Adjuntos.Count == 0 && Archivo == null && string.IsNullOrWhiteSpace(UrlArchivo))
            yield return new ValidationResult("Debe subir un archivo o indicar una URL.", [nameof(Archivo), nameof(UrlArchivo)]);

        if (Archivo != null && !string.IsNullOrWhiteSpace(UrlArchivo))
            yield return new ValidationResult("Debe enviar un archivo o una URL, no ambos.", [nameof(Archivo), nameof(UrlArchivo)]);

        if (Archivo != null && Archivo.Length > 50 * 1024 * 1024)
            yield return new ValidationResult("El archivo no puede superar los 50 MB.", [nameof(Archivo)]);

        if (!string.IsNullOrWhiteSpace(UrlArchivo) &&
            (!Uri.TryCreate(UrlArchivo, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult("La URL debe ser HTTPS.", [nameof(UrlArchivo)]);
        }
    }
}
