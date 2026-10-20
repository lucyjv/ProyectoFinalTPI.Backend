using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Grupos;

public class BuscarGruposRequest
{
    [Required(ErrorMessage = "Debe indicar un texto de busqueda.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El texto de busqueda debe tener entre 2 y 100 caracteres.")]
    public string Texto { get; set; } = string.Empty;

    [Range(1, 50, ErrorMessage = "El limite debe estar entre 1 y 50.")]
    public int Limite { get; set; } = 20;
}
