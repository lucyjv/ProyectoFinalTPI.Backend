using ProyectoFinalTPI.Backend.Dtos.Reportes;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IReporteServicio
{
    Task<Reporte> Crear(
        int reportanteId, CrearReporteDto dto, CancellationToken cancellationToken = default);
}
