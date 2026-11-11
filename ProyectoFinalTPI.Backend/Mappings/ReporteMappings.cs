using ProyectoFinalTPI.Backend.Dtos.Reportes;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Mappings;

public static class ReporteMappings
{
    public static ReporteCreadoDto ACreadoDto(this Reporte reporte) => new()
    {
        Id = reporte.Id,
        Estado = reporte.Estado,
        FechaCreacion = reporte.FechaCreacion
    };
}
