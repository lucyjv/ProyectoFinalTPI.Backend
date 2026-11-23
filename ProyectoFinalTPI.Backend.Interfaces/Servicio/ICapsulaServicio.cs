using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Capsulas;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface ICapsulaServicio
{
    Task<ResultadoPaginadoDto<CapsulaDto>> Listar(int propietarioId, bool propias, string? cursor, int limite, CancellationToken ct);
    Task<CapsulaDto> Obtener(int id, int? lectorId, CancellationToken ct);
    Task<CapsulaDto> Crear(int propietarioId, CrearCapsulaDto datos, CancellationToken ct);
    Task<CapsulaDto> Actualizar(int id, int propietarioId, ActualizarCapsulaDto datos, CancellationToken ct);
    Task<CapsulaDto> CrearConPortada(int propietarioId, CrearCapsulaDto datos, Stream archivo, string contentType, CancellationToken ct);
    Task<CapsulaDto> ActualizarConPortada(int id, int propietarioId, ActualizarCapsulaDto datos, Stream archivo, string contentType, CancellationToken ct);
    Task<byte[]> ObtenerPortada(int id, int? lectorId, CancellationToken ct);
    Task Eliminar(int id, int propietarioId, CancellationToken ct);
    Task AgregarRecuerdos(int id, int propietarioId, IReadOnlyCollection<int> publicacionesIds, CancellationToken ct);
    Task QuitarRecuerdo(int id, int propietarioId, int publicacionId, CancellationToken ct);
    Task<ResultadoPaginadoDto<RecuerdoDisponibleCapsulaDto>> ObtenerDisponibles(int propietarioId, int? capsulaId,
        string? cursor, int limite, CancellationToken ct);
    Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerRecuerdos(int id, int? lectorId, string? cursor,
        int limite, CancellationToken ct);
    Task<MapaCapsulaDto> ObtenerMapa(int id, int? lectorId, ConsultaMapaCapsulaDto filtro, string? cursor,
        int limite, CancellationToken ct);
}
