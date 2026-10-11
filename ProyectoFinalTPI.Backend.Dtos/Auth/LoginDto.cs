using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Dtos.Auth
{
    public class LoginDto
    {
        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Contraseña { get; set; } = string.Empty;
    }
}
