using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AdminEstadisticaServicio(IAdminEstadisticaRepositorio repositorio)
    : IAdminEstadisticaServicio
{
    public Task<EstadisticasAdminDto> Obtener(
        int limiteRanking, CancellationToken cancellationToken = default)
    {
        if (limiteRanking is < 1 or > 50)
            throw new ArgumentException("El límite del ranking debe estar entre 1 y 50.");
        return repositorio.Obtener(limiteRanking, cancellationToken);
    }
}
