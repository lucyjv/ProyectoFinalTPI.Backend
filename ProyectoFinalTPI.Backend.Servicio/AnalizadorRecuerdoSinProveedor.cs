using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AnalizadorRecuerdoSinProveedor : IAnalizadorRecuerdo
{
    public bool Disponible => false;
    public string Version => "sin-proveedor-v1";
    public Task<ResultadoAnalisisRecuerdoDto> Analizar(EntradaAnalisisRecuerdoDto entrada, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // No inventar asociaciones ni retirar las existentes sin haber analizado el texto.
        return Task.FromResult(new ResultadoAnalisisRecuerdoDto(Array.Empty<MomentoDetectadoDto>(), AnalisisRealizado: false));
    }
}
