using ProyectoFinalTPI.Backend.Dtos.Momento;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IAnalisisRecuerdosServicio
{
    Task<bool> ProcesarUno(CancellationToken ct);
    Task<EstadoAnalisisRecuerdoDto> ObtenerEstado(int id, CancellationToken ct);
}
