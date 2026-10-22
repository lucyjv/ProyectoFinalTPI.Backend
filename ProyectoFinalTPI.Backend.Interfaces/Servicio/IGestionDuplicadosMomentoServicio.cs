using ProyectoFinalTPI.Backend.Dtos.Momento;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IGestionDuplicadosMomentoServicio
{
    Task<IReadOnlyList<CandidatoDuplicadoMomentoDto>> BuscarCandidatos(int id, int limite, CancellationToken ct);
    Task<VistaFusionMomentoDto> Previsualizar(int origenId, int destinoId, CancellationToken ct);
    Task<ResultadoFusionMomentoDto> Fusionar(int origenId, SolicitudFusionMomentoDto solicitud, int administradorId, CancellationToken ct);
}
