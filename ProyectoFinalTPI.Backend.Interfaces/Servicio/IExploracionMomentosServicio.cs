using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IExploracionMomentosServicio
{
    Task<ResultadoPaginadoDto<MomentoResumenDto>> Buscar(string? busqueda, string? cursor, int limite, CancellationToken ct);
    Task<MomentoDetalleDto> ObtenerDetalle(int id, CancellationToken ct);
    Task<ResultadoPaginadoDto<PublicacionResumenDto>> ObtenerPublicaciones(int id, bool descendientes, string? cursor, int limite, CancellationToken ct);
    Task<MomentosPublicacionDto> ObtenerEstado(int id, int? usuarioId, CancellationToken ct);
    Task<MomentosPublicacionDto> Corregir(int id, int usuarioId, IReadOnlyCollection<int> momentoIds, bool permitirAutomaticos, long version, CancellationToken ct);
}
