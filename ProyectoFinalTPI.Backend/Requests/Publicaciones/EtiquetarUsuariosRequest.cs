using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Publicaciones
{
    public class EtiquetarUsuariosRequest : IValidatableObject
    {
        [Required]
        public List<int> UsuariosIds { get; set; } = new();


        //valida el cuerpo de la peticion
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (UsuariosIds is null)
            {
                yield return new ValidationResult(
                    "Debe indicar los IDs de los usuarios",
                    new[] { nameof(UsuariosIds) });
                yield break;
            }

            if (UsuariosIds.Count == 0)
            {
                yield return new ValidationResult(
                    "Debe indicar al menos un usuario",
                    new[] { nameof(UsuariosIds) });
            }

            if (UsuariosIds.Any(id => id <= 0))
            {
                yield return new ValidationResult(
                    "Los IDs de usuarios son erroneos",
                    new[] { nameof(UsuariosIds) });
            }
        }
    }
}
