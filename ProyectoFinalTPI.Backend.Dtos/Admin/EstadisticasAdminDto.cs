namespace ProyectoFinalTPI.Backend.Dtos.Admin;

public class EstadisticasAdminDto
{
    public int TotalUsuarios { get; set; }
    public int TotalPublicaciones { get; set; }
    public int PublicacionesVisibles { get; set; }
    public int PublicacionesOcultas { get; set; }
    public List<CategoriaEstadisticaDto> PublicacionesPorCategoria { get; set; } = [];
    public List<PublicacionRankingDto> MasGustadas { get; set; } = [];
    public List<PublicacionRankingDto> MasPuntuadas { get; set; } = [];
}

public class CategoriaEstadisticaDto
{
    public string Categoria { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}

public class PublicacionRankingDto
{
    public int PublicacionId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string AutorUsername { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}
