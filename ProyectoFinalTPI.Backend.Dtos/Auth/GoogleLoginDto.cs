using System.ComponentModel.DataAnnotations;
namespace ProyectoFinalTPI.Backend.Dtos.Auth;

public sealed class GoogleLoginDto
{
    [Required, StringLength(10000)]
    public string Credential { get; set; } = "";
    [StringLength(50, MinimumLength = 3)]
    public string? Username { get; set; }
}

// Created exclusively by the server after cryptographic token validation.
public sealed record IdentidadGoogle(string Subject, string Email, bool CorreoAcreditado);
