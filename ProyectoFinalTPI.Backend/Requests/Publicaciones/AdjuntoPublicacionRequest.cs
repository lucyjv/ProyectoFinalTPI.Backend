using System.ComponentModel.DataAnnotations;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;

namespace ProyectoFinalTPI.Backend.Requests.Publicaciones;

public sealed class AdjuntoPublicacionRequest : IValidatableObject
{
    public IFormFile? Archivo { get; set; }
    public string? Url { get; set; }
    [EnumDataType(typeof(MultimediaEnum))]
    public MultimediaEnum Tipo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if ((Archivo is null) == string.IsNullOrWhiteSpace(Url))
            yield return new("Cada adjunto requiere un archivo o una URL, exclusivamente.");
        if (Archivo is not null && (Archivo.Length == 0 || Archivo.Length > 50 * 1024 * 1024))
            yield return new("Cada archivo debe tener contenido y ocupar hasta 50 MB.");
        if (!string.IsNullOrWhiteSpace(Url) && (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            yield return new("La URL del adjunto debe ser HTTPS.");
    }
}

// Los streams pertenecen a la solicitud HTTP y se cierran aun cuando falle la creación.
public sealed class AdjuntosFormulario : IDisposable
{
    public IReadOnlyList<AdjuntoEntradaDto>? Elementos { get; }
    public AdjuntosFormulario(IReadOnlyCollection<AdjuntoPublicacionRequest> solicitudes)
    {
        var elementos = new List<AdjuntoEntradaDto>();
        try
        {
            foreach (var solicitud in solicitudes)
                elementos.Add(new(solicitud.Archivo?.OpenReadStream(), solicitud.Archivo?.FileName, solicitud.Url, solicitud.Tipo));
            Elementos = elementos.Count == 0 ? null : elementos;
        }
        catch
        {
            foreach (var elemento in elementos) elemento.Archivo?.Dispose();
            throw;
        }
    }
    public void Dispose()
    {
        if (Elementos is not null) foreach (var elemento in Elementos) elemento.Archivo?.Dispose();
    }
}