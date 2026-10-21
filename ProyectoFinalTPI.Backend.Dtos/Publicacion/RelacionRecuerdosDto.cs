using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion;

public class RecuerdoRelacionadoDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string DescripcionBreve { get; set; } = "";
    public DateTime Fecha { get; set; }
    public string UrlMultimedia { get; set; } = "";
    public MultimediaEnum TipoMultimedia { get; set; }
    public int AutorId { get; set; }
    public string AutorUsername { get; set; } = "";
    public string NombreLugar { get; set; } = "";
    public double DistanciaMetros { get; set; }
    public bool MismoDia { get; set; }
    public bool MismoLugar => true;
}

public class RelacionesRecuerdoDto
{
    public int PublicacionId { get; set; }
    public int RadioMetros => 200;
    public ResultadoPaginadoDto<RecuerdoRelacionadoDto> Dia { get; set; } = new();
    public ResultadoPaginadoDto<RecuerdoRelacionadoDto> Lugar { get; set; } = new();
}

public record CursorRelacionDto(int PublicacionId, string Tipo, double DistanciaMetros, int Id);
