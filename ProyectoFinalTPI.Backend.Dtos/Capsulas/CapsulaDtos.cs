using System.ComponentModel.DataAnnotations;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Capsulas;

public class ActualizarCapsulaDto
{
    [Required, StringLength(100, MinimumLength = 3)]
    public string Nombre { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Descripcion { get; set; }
    [EnumDataType(typeof(VisibilidadCapsula))]
    public VisibilidadCapsula Visibilidad { get; set; } = VisibilidadCapsula.Privada;
    [Range(1, int.MaxValue)]
    public int? PortadaPublicacionId { get; set; }
    [Range(1, int.MaxValue)]
    public int? PortadaAdjuntoId { get; set; }
    public bool ConservarPortadaSubida { get; set; } = true;
}

public sealed class CrearCapsulaDto : ActualizarCapsulaDto
{
    [MaxLength(100)]
    public List<int> PublicacionesIds { get; set; } = new();
}

public sealed class AsociarRecuerdosCapsulaDto
{
    [Required, MinLength(1), MaxLength(100)]
    public List<int> PublicacionesIds { get; set; } = new();
}

public sealed class CapsulaDto
{
    public int Id { get; set; }
    public int PropietarioId { get; set; }
    public string PropietarioUsername { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public VisibilidadCapsula Visibilidad { get; set; }
    public int? PortadaPublicacionId { get; set; }
    public int? PortadaAdjuntoId { get; set; }
    public string? PortadaUrl { get; set; }
    public bool PortadaSubida { get; set; }
    public int CantidadRecuerdos { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
    public bool PuedeAdministrar { get; set; }
}

public sealed class RecuerdoDisponibleCapsulaDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? PortadaUrl { get; set; }
    public bool YaAgregado { get; set; }
}

public sealed record ReferenciaRecuerdoCapsulaDto(int Id, DateTime Fecha);
public sealed record LimitesMapaCapsulaDto(double MinLatitud, double MaxLatitud, double MinLongitud, double MaxLongitud);

public sealed class MapaCapsulaDto : ResultadoPaginadoDto<PublicacionPinDto>
{
    public LimitesMapaCapsulaDto? Limites { get; set; }
}

public sealed class ConsultaMapaCapsulaDto
{
    public double? MinLatitud { get; set; }
    public double? MaxLatitud { get; set; }
    public double? MinLongitud { get; set; }
    public double? MaxLongitud { get; set; }
}
