using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.Extensions;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AdminEstadisticaServicio(IAdminEstadisticaRepositorio repositorio)
    : IAdminEstadisticaServicio
{
    public Task<EstadisticasAdminDto> Obtener(
        int limiteRanking, CancellationToken cancellationToken = default)
    {
        limiteRanking.ValidarRango(1, 50, "límite del ranking");
        return repositorio.Obtener(limiteRanking, cancellationToken);
    }
}
