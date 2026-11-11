using ProyectoFinalTPI.Backend.Dtos.Admin;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IAdminReporteServicio
{
    Task<PaginaAdminDto<ReporteAdminDto>> Listar(
        FiltroReportesAdminDto filtro, CancellationToken cancellationToken = default);
    Task Resolver(
        int administradorId, ResolverReporteAdminDto dto,
        CancellationToken cancellationToken = default);
}
