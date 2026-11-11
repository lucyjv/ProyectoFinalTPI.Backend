using System.ComponentModel.DataAnnotations;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Reportes;

public class CrearReporteDto
{
    [Required]
    public TipoContenidoReporte TipoContenido { get; set; }

    [Range(1, int.MaxValue)]
    public int ContenidoId { get; set; }

    [Required]
    public MotivoReporte Motivo { get; set; }

    [MaxLength(1000)]
    public string? Detalle { get; set; }
}

public class ReporteCreadoDto
{
    public int Id { get; set; }
    public EstadoReporte Estado { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class ObjetivoReporteDto
{
    public int UsuarioReportadoId { get; set; }
    public string ContenidoSnapshot { get; set; } = string.Empty;
    public int? ContextoId { get; set; }
    public string? Contexto { get; set; }
}
