using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion;

// Datos para ordenar recomendaciones. El detalle se consulta al construir la página.
public sealed class CaracteristicasFeedDto
{
    public int Id { get; set; }
    public ReputacionEnum? ReputacionAutor { get; set; }
    public bool EsCuentaMarca { get; set; }
    public CategoriaEnum Categoria { get; set; }
    public DateTime FechaCreacion { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
}

public sealed record LimitesExploracionFeedDto(int VecinosPorSemilla, int PublicacionesPorVecino);

public sealed class ResumenInteraccionesFeedDto
{
    public int CantidadLikes { get; set; }
    public bool DioLike { get; set; }
    public int PuntosTotales { get; set; }
    public int? MiPuntuacion { get; set; }
    public int CantidadReposts { get; set; }
    public bool DioRepost { get; set; }
    public bool Guardada { get; set; }
}
