using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Dtos.Auth;

public sealed class GoogleLoginDto
{
    [Required, StringLength(8192)]
    public string Credential { get; init; } = string.Empty;

    [StringLength(100)]
    public string? Contrasena { get; init; }

    public bool ConfirmarCreacion { get; init; }

    [StringLength(50, ErrorMessage = "El nombre de usuario no puede superar los 50 caracteres.")]
    public string? Username { get; init; }
}

public sealed record IdentidadGoogleDto(string Subject, string Email);
