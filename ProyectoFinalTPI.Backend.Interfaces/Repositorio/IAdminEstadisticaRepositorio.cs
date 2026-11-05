using ProyectoFinalTPI.Backend.Dtos.Admin;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IAdminEstadisticaRepositorio
{
    Task<EstadisticasAdminDto> Obtener(int limiteRanking, CancellationToken cancellationToken = default);
}
