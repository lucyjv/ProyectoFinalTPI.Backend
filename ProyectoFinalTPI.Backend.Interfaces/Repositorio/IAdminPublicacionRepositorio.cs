using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IAdminPublicacionRepositorio
{
    Task<PaginaAdminDto<PublicacionAdminDto>> Listar(FiltroPublicacionesAdminDto filtro, CancellationToken cancellationToken = default);
    Task<Publicacion?> Obtener(int id, CancellationToken cancellationToken = default);
    Task Guardar(CancellationToken cancellationToken = default);
    Task Eliminar(Publicacion publicacion, CancellationToken cancellationToken = default);
}
