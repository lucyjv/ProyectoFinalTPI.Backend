using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Dtos.Usuario;

public sealed class CambiarUsernameDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;
}
