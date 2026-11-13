using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Dtos.Auth;

public sealed class RecuperarContrasenaDto
{
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;
}

public sealed class RestablecerContrasenaDto
{
    [Required(ErrorMessage = "El código de recuperación es obligatorio.")]
    [StringLength(200)]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 100 caracteres.")]
    public string NuevaContrasena { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tenés que confirmar la contraseña.")]
    [StringLength(100)]
    [Compare(nameof(NuevaContrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmacionContrasena { get; set; } = string.Empty;
}
