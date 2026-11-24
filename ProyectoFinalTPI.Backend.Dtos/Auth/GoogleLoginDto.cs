using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Dtos.Auth;

public sealed class GoogleLoginDto
{
    [Required, StringLength(8192)]
    public string Credential { get; init; } = string.Empty;

    [StringLength(100)]
    public string? Contrasena { get; init; }
}

public sealed record IdentidadGoogleDto(string Subject, string Email);
