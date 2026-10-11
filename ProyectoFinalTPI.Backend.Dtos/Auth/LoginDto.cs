using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProyectoFinalTPI.Backend.Dtos.Auth
{
    public class LoginDto
    {
        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
        public string Email { get; set; } = string.Empty;

        private string _contrasena = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Contraseña
        {
            get => _contrasena;
            set => _contrasena = value;
        }

        [JsonPropertyName("contrasena")]
        public string? ContrasenaAlias
        {
            get => _contrasena;
            set { if (!string.IsNullOrEmpty(value)) _contrasena = value; }
        }
    }
}
