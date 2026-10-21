using ProyectoFinalTPI.Backend.Dtos.Momento;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IReprocesamientoRecuerdosServicio
{
    Task<VistaPreviaReprocesamientoDto> Previsualizar(int limite, int despuesDeId, bool incluirFallidos, CancellationToken ct);
    Task<ResultadoReprocesamientoDto> Encolar(SolicitudReprocesamientoDto solicitud, int administradorId, CancellationToken ct);
}
