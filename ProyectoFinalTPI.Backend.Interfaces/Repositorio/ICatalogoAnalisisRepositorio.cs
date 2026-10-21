using ProyectoFinalTPI.Backend.Dtos.Momento;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface ICatalogoAnalisisRepositorio
{
    Task<IReadOnlyList<CandidatoMomentoDto>> BuscarCandidatos(string texto, CancellationToken ct);
}
