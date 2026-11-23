using ProyectoFinalTPI.Backend.Dtos.Capsulas;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface ICapsulaRepositorio
{
    Task<List<CapsulaDto>> Listar(int propietarioId, bool incluirPrivadas, int? antesId, int cantidad, CancellationToken ct);
    Task<CapsulaDto?> Obtener(int id, int? lectorId, CancellationToken ct);
    Task<int> Crear(Capsula capsula, IReadOnlyCollection<int> publicacionesIds, CancellationToken ct);
    Task<string?> Actualizar(int id, int propietarioId, ActualizarCapsulaDto datos, CancellationToken ct);
    Task<string?> ActualizarConPortada(int id, int propietarioId, ActualizarCapsulaDto datos, string recursoId, CancellationToken ct);
    Task<string?> Eliminar(int id, int propietarioId, CancellationToken ct);
    Task<string?> ObtenerRecursoPortada(int id, int? lectorId, CancellationToken ct);
    Task AgregarRecuerdos(int id, int propietarioId, IReadOnlyCollection<int> publicacionesIds, CancellationToken ct);
    Task QuitarRecuerdo(int id, int propietarioId, int publicacionId, CancellationToken ct);
    Task<List<RecuerdoDisponibleCapsulaDto>> ObtenerDisponibles(int propietarioId, int? capsulaId,
        DateTime? antesFecha, int? antesId, int cantidad, CancellationToken ct);
    Task<List<ReferenciaRecuerdoCapsulaDto>> ObtenerReferencias(int id, int? lectorId,
        DateTime? desdeFecha, int? desdeId, int cantidad, CancellationToken ct);
    Task<List<PublicacionPinDto>> ObtenerPines(int id, int? lectorId, ConsultaMapaCapsulaDto filtro,
        int? despuesId, int cantidad, CancellationToken ct);
    Task<LimitesMapaCapsulaDto?> ObtenerLimites(int id, int? lectorId, CancellationToken ct);
    Task ValidarParaPublicar(int propietarioId, IReadOnlyCollection<int> capsulasIds, CancellationToken ct);
}
