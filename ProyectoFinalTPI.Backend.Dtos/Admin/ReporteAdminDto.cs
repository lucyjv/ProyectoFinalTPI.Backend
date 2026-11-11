using System.ComponentModel.DataAnnotations;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Admin;

public class ReporteAdminDto
{
    public int Id { get; set; }
    public TipoContenidoReporte TipoContenido { get; set; }
    public int ContenidoId { get; set; }
    public MotivoReporte Motivo { get; set; }
    public string? Detalle { get; set; }
    public string ContenidoSnapshot { get; set; } = string.Empty;
    public int? ContextoId { get; set; }
    public string? Contexto { get; set; }
    public EstadoReporte Estado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int ReportanteId { get; set; }
    public string ReportanteUsername { get; set; } = string.Empty;
    public int UsuarioReportadoId { get; set; }
    public string UsuarioReportadoUsername { get; set; } = string.Empty;
    public int CantidadCoincidentes { get; set; }
    public int? AdministradorId { get; set; }
    public string? AdministradorUsername { get; set; }
    public DateTime? FechaResolucion { get; set; }
    public AccionReporte? AccionTomada { get; set; }
    public string? ObservacionAdministrativa { get; set; }
}

public class FiltroReportesAdminDto
{
    public int Pagina { get; set; } = 1;
    public int Tamanio { get; set; } = 20;
    public string? Texto { get; set; }
    public TipoContenidoReporte? TipoContenido { get; set; }
    public MotivoReporte? Motivo { get; set; }
    public EstadoReporte? Estado { get; set; } = EstadoReporte.Pendiente;
}

public class ResolverReporteAdminDto
{
    [Range(1, int.MaxValue)]
    public int ReporteId { get; set; }

    [Required]
    public AccionReporte Accion { get; set; }

    [MaxLength(1000)]
    public string? Observacion { get; set; }

    [Range(1, 365)]
    public int DiasSuspension { get; set; } = 7;
}
