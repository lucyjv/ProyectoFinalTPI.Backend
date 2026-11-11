namespace ProyectoFinalTPI.Backend.Entidades;

public class Reporte
{
    public int Id { get; set; }
    public int ReportanteId { get; set; }
    public Usuario Reportante { get; set; } = null!;
    public int UsuarioReportadoId { get; set; }
    public Usuario UsuarioReportado { get; set; } = null!;
    public TipoContenidoReporte TipoContenido { get; set; }
    public int ContenidoId { get; set; }
    public MotivoReporte Motivo { get; set; }
    public string? Detalle { get; set; }
    public string ContenidoSnapshot { get; set; } = string.Empty;
    public int? ContextoId { get; set; }
    public string? Contexto { get; set; }
    public EstadoReporte Estado { get; set; } = EstadoReporte.Pendiente;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int? AdministradorId { get; set; }
    public Usuario? Administrador { get; set; }
    public DateTime? FechaResolucion { get; set; }
    public AccionReporte? AccionTomada { get; set; }
    public string? ObservacionAdministrativa { get; set; }
}

public enum TipoContenidoReporte
{
    Publicacion,
    Comentario,
    MensajeChat,
    Perfil
}

public enum MotivoReporte
{
    Spam,
    Acoso,
    ContenidoOfensivo,
    Desinformacion,
    Suplantacion,
    ContenidoSexual,
    Violencia,
    Otro
}

public enum EstadoReporte
{
    Pendiente,
    EnRevision,
    Resuelto,
    Desestimado
}

public enum AccionReporte
{
    Desestimar,
    OcultarContenido,
    EliminarContenido,
    SuspenderUsuario,
    DarDeBajaUsuario
}

public class ReporteDuplicadoException(string mensaje) : InvalidOperationException(mensaje);
