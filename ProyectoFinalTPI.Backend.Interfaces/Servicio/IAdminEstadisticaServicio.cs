using ProyectoFinalTPI.Backend.Dtos.Admin;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IAdminEstadisticaServicio
{
    Task<EstadisticasAdminDto> Obtener(int limiteRanking, CancellationToken cancellationToken = default);
}
