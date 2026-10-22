using ProyectoFinalTPI.Backend.Dtos.Momento;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IReprocesamientoRecuerdosRepositorio
{
    Task<VistaPreviaReprocesamientoDto> Previsualizar(int limite, int despuesDeId, bool incluirFallidos, CancellationToken ct);
    Task<ResultadoReprocesamientoDto> Encolar(SolicitudReprocesamientoDto solicitud, DateTime ahora, CancellationToken ct);
}
