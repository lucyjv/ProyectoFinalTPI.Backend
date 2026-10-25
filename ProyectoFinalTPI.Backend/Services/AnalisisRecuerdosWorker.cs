using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;

namespace ProyectoFinalTPI.Backend.Services;

public class AnalisisRecuerdosWorker(IServiceScopeFactory scopes, IOptions<OpcionesAnalisisRecuerdos> opciones,
    TimeProvider reloj, ILogger<AnalisisRecuerdosWorker> logger) : BackgroundService
{
    public async Task<bool> EjecutarCiclo(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAnalisisRecuerdosServicio>().ProcesarUno(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opciones.Value.Habilitado) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await EjecutarCiclo(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                logger.LogWarning("No se pudo consultar/procesar la cola de recuerdos. Se reintentará.");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(opciones.Value.IntervaloSegundos), reloj, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
