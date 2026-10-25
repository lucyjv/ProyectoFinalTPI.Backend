using ProyectoFinalTPI.Backend.Dtos.Momento;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IAnalizadorRecuerdo
{
    bool Disponible => true;
    string Version { get; }
    // Debe respetar cancelación y devolver datos, sin escribir asociaciones por su cuenta.
    // Puede devolver IDs resueltos o propuestas: el repositorio confirma el catálogo y las asociaciones atómicamente.
    Task<ResultadoAnalisisRecuerdoDto> Analizar(EntradaAnalisisRecuerdoDto entrada, CancellationToken ct);
}
