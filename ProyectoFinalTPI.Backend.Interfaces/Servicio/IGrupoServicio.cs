using ProyectoFinalTPI.Backend.Dtos.Grupo;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IGrupoServicio
{
    Task<IReadOnlyList<GrupoResumenDto>> ObtenerTodos(int? usuarioActualId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GrupoResumenDto>> Buscar(
        string texto, int limite, int? usuarioActualId, CancellationToken cancellationToken = default);
    Task<GrupoResumenDto> ObtenerPorId(int grupoId, int? usuarioActualId, CancellationToken cancellationToken = default);
    Task<GrupoResumenDto> Crear(Grupo grupo, CancellationToken cancellationToken = default);
    Task<GrupoResumenDto> Actualizar(int grupoId, int usuarioId, Grupo cambios, CancellationToken cancellationToken = default);
    Task Eliminar(int grupoId, int usuarioId, CancellationToken cancellationToken = default);
    Task Unirse(int grupoId, int usuarioId, CancellationToken cancellationToken = default);
    Task Salir(int grupoId, int usuarioId, CancellationToken cancellationToken = default);
    Task Expulsar(int grupoId, int usuarioIdCreador, int usuarioIdAEliminar, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GrupoIntegranteDto>> ObtenerIntegrantes(int grupoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicacionDetalleDto>> ObtenerRecuerdos(int grupoId, CancellationToken cancellationToken = default);
    Task<int> CrearRecuerdo(int grupoId, int usuarioId, Publicacion publicacion, Stream? archivo = null,
        string? nombreArchivo = null, CancellationToken cancellationToken = default);
}
