using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IGrupoRepositorio
{
    Task<List<Grupo>> ObtenerTodos(CancellationToken cancellationToken = default);
    Task<List<Grupo>> Buscar(string texto, int limite, CancellationToken cancellationToken = default);
    Task<Grupo?> ObtenerPorId(int grupoId, CancellationToken cancellationToken = default);
    Task<int> Crear(Grupo grupo, CancellationToken cancellationToken = default);
    Task Actualizar(Grupo grupo, CancellationToken cancellationToken = default);
    Task Eliminar(Grupo grupo, CancellationToken cancellationToken = default);
    Task<bool> EsIntegrante(int grupoId, int usuarioId, CancellationToken cancellationToken = default);
    Task<GrupoIntegrante?> ObtenerIntegrante(int grupoId, int usuarioId, CancellationToken cancellationToken = default);
    Task AgregarIntegrante(GrupoIntegrante integrante, CancellationToken cancellationToken = default);
    Task QuitarIntegrante(GrupoIntegrante integrante, CancellationToken cancellationToken = default);
    Task<List<GrupoIntegrante>> ObtenerIntegrantes(int grupoId, CancellationToken cancellationToken = default);
    Task<List<PublicacionDetalleDto>> ObtenerRecuerdos(int grupoId, CancellationToken cancellationToken = default);
}
