using ProyectoFinalTPI.Backend.Dtos.Publicacion;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IRelacionRecuerdosRepositorio
{
    Task<int?> ObtenerOriginalVisible(int id, CancellationToken ct);
    Task<List<RecuerdoRelacionadoDto>> Obtener(int id, string tipo, CursorRelacionDto? cursor, int limite, CancellationToken ct);
}
