namespace ProyectoFinalTPI.Backend.Dtos.Momento;

public class MomentoResumenDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public int? MomentoPadreId { get; set; }
}

public class MomentoDetalleDto : MomentoResumenDto
{
    public long VersionCatalogo { get; set; }
    public int? RedirigidoDesdeId { get; set; }
    public string Descripcion { get; set; } = "";
    public MomentoResumenDto? Padre { get; set; }
    public List<MomentoResumenDto> Hijos { get; set; } = new();
    public List<string> Alias { get; set; } = new();
}

public class MomentoAsociadoDto
{
    public MomentoResumenDto Momento { get; set; } = new();
    public string Origen { get; set; } = "";
}

public class MomentosPublicacionDto
{
    public int PublicacionId { get; set; }
    public long VersionRelaciones { get; set; }
    public bool PermitirMomentosAutomaticos { get; set; }
    public bool PuedeCorregir { get; set; }
    public List<MomentoAsociadoDto> Momentos { get; set; } = new();
}

public record CursorExploracionMomentoDto(string Tipo, int Ambito, string Busqueda, bool Descendientes, DateTime? Fecha, int Id);
