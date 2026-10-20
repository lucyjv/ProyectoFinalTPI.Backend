using System.ComponentModel.DataAnnotations;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;

namespace ProyectoFinalTPI.Backend.Requests.Grupos;

public class ActualizarGrupoRequest : IValidatableObject
{
    [Required(ErrorMessage = "Debe indicar el nombre del grupo.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripcion no puede superar los 500 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Range(1900, 2100, ErrorMessage = "La decada debe ser un año entre 1900 y 2100.")]
    public int Decada { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ValidationResult? error = null;
        try
        {
            _ = new Decada(Decada);
        }
        catch (ArgumentException exception)
        {
            error = new ValidationResult(exception.Message, [nameof(Decada)]);
        }
        if (error is not null) yield return error;
    }
}
