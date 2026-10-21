using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Publicaciones;

public class CorregirMomentosRequest
{
    [Required, MaxLength(50)]
    public List<int>? MomentoIds { get; set; }
    [Required]
    public bool? PermitirMomentosAutomaticos { get; set; }
    [Required, Range(0, long.MaxValue)]
    public long? VersionRelaciones { get; set; }
}
