using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion;

public class InteraccionFeedDto
{
    public int PublicacionId { get; set; }
    public bool DioLike { get; set; }
    public int? MiPuntuacion { get; set; }
    public long? UltimaImpresionUnixMs { get; set; }
}

public class RecomendacionPublicacionDto
{
    public int PublicacionId { get; set; }
    public double PuntajeColaborativo { get; set; }
}
