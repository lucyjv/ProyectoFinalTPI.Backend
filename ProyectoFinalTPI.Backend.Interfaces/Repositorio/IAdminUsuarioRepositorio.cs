using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IAdminUsuarioRepositorio
{
    Task<PaginaAdminDto<UsuarioAdminDto>> Listar(FiltroUsuariosAdminDto filtro, CancellationToken cancellationToken = default);
    Task<Usuario?> Obtener(int id, CancellationToken cancellationToken = default);
    Task Guardar(CancellationToken cancellationToken = default);
}
