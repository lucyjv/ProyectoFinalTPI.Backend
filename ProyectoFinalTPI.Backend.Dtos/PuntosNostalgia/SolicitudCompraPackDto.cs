using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Dtos.PuntosNostalgia;

public sealed class SolicitudCompraPackDto
{
    [Required(ErrorMessage = "El identificador del pack es obligatorio.")]
    public string PackId { get; set; } = string.Empty;
}
