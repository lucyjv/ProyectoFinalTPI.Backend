using ProyectoFinalTPI.Backend.Dtos.Momento;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IGestionDuplicadosMomentoRepositorio
{
    Task<IReadOnlyList<CandidatoDuplicadoMomentoDto>> BuscarCandidatos(int id, int limite, CancellationToken ct);
    Task<VistaFusionMomentoDto> Previsualizar(int origenId, int destinoId, CancellationToken ct);
    Task<ResultadoFusionMomentoDto> Fusionar(int origenId, SolicitudFusionMomentoDto solicitud, CancellationToken ct);
}
