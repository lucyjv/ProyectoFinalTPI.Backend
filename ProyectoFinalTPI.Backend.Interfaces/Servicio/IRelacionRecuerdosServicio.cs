using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IRelacionRecuerdosServicio
{
    Task<RelacionesRecuerdoDto> ObtenerRelaciones(int id, CancellationToken ct);
    Task<ResultadoPaginadoDto<RecuerdoRelacionadoDto>> ObtenerRelacionadas(int id, string tipo, string? cursor, int limite, CancellationToken ct);
}
