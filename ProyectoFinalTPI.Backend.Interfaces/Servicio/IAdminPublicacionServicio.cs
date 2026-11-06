using ProyectoFinalTPI.Backend.Dtos.Admin;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IAdminPublicacionServicio
{
    Task<PaginaAdminDto<PublicacionAdminDto>> Listar(FiltroPublicacionesAdminDto filtro, CancellationToken cancellationToken = default);
    Task<PublicacionAdminDto> Actualizar(int id, ActualizarPublicacionAdminDto dto, CancellationToken cancellationToken = default);
    Task CambiarVisibilidad(int id, CambiarVisibilidadPublicacionDto dto, CancellationToken cancellationToken = default);
    Task Eliminar(int id, CancellationToken cancellationToken = default);
}
