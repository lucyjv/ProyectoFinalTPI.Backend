using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion;

public class PuntuarPublicacionDto
{
    [Range(1, 5, ErrorMessage = "La puntuación debe estar comprendida entre 1 y 5.")]
    public int Puntaje { get; set; }
}
