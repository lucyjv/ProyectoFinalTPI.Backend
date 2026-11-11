using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Dtos.Reportes;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IReporteRepositorio
{
    Task<ObjetivoReporteDto?> ObtenerObjetivo(
        TipoContenidoReporte tipo, int contenidoId, int reportanteId,
        CancellationToken cancellationToken = default);
    Task<bool> ExisteActivo(
        int reportanteId, TipoContenidoReporte tipo, int contenidoId,
        CancellationToken cancellationToken = default);
    Task Agregar(Reporte reporte, CancellationToken cancellationToken = default);
    Task<PaginaAdminDto<ReporteAdminDto>> Listar(
        FiltroReportesAdminDto filtro, CancellationToken cancellationToken = default);
    Task<Reporte?> Obtener(int id, CancellationToken cancellationToken = default);
    Task EliminarMensajeChat(int id, CancellationToken cancellationToken = default);
    Task ResolverRelacionados(
        Reporte reporte, int administradorId, AccionReporte accion, string? observacion,
        CancellationToken cancellationToken = default);
}
