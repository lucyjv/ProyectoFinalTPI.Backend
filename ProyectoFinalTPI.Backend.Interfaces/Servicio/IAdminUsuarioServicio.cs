using ProyectoFinalTPI.Backend.Dtos.Admin;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IAdminUsuarioServicio
{
    Task<PaginaAdminDto<UsuarioAdminDto>> Listar(FiltroUsuariosAdminDto filtro, CancellationToken cancellationToken = default);
    Task CambiarSuspension(int id, int adminId, CambiarSuspensionUsuarioDto dto, CancellationToken cancellationToken = default);
    Task DarDeBaja(int id, int adminId, CancellationToken cancellationToken = default);
}
