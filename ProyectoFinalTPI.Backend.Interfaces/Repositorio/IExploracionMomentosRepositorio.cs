using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IExploracionMomentosRepositorio
{
    Task<List<MomentoResumenDto>> Buscar(string texto, string textoNormalizado, int? despuesId, int limite, CancellationToken ct);
    Task<MomentoDetalleDto?> ObtenerDetalle(int id, CancellationToken ct);
    Task<List<PublicacionResumenDto>> ObtenerPublicaciones(int id, bool descendientes, CursorExploracionMomentoDto? cursor, int limite, CancellationToken ct);
    Task<MomentosPublicacionDto?> ObtenerEstado(int id, int? usuarioId, CancellationToken ct);
    Task<MomentosPublicacionDto> Corregir(int id, int usuarioId, IReadOnlyCollection<int> momentoIds, bool permitirAutomaticos, long version, CancellationToken ct);
}
