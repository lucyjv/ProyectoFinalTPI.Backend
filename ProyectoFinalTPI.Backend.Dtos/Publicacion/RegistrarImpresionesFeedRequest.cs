namespace ProyectoFinalTPI.Backend.Dtos.Publicacion;

public class RegistrarImpresionesFeedRequest
{
    public IReadOnlyCollection<int>? PublicacionesIds { get; set; } = Array.Empty<int>();
}
