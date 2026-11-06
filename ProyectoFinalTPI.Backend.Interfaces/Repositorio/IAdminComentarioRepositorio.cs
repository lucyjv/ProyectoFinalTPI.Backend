using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IAdminComentarioRepositorio
{
    Task<Comentario?> Obtener(int id, CancellationToken cancellationToken = default);
    Task Eliminar(Comentario comentario, CancellationToken cancellationToken = default);
}
