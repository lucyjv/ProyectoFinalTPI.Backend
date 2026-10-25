using ProyectoFinalTPI.Backend.Dtos.Momento;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface ITrabajoAnalisisRepositorio
{
    Task<ReservaAnalisisDto?> Reservar(DateTime ahora, TimeSpan duracion, int maxIntentos, CancellationToken ct);
    Task<EntradaAnalisisRecuerdoDto?> ObtenerEntrada(ReservaAnalisisDto reserva, DateTime ahora, CancellationToken ct);
    Task<bool> Completar(ReservaAnalisisDto reserva, ResultadoAnalisisRecuerdoDto resultado, string versionAnalizador, DateTime ahora, CancellationToken ct);
    Task Descartar(ReservaAnalisisDto reserva, DateTime ahora, CancellationToken ct);
    Task RegistrarFallo(ReservaAnalisisDto reserva, string codigo, DateTime ahora, DateTime siguienteIntento, int maxIntentos, CancellationToken ct);
    Task<EstadoAnalisisRecuerdoDto?> ObtenerEstado(int publicacionId, CancellationToken ct);
}
