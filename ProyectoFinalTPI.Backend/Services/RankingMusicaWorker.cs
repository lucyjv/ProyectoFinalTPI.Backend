using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Services;

public sealed class RankingMusicaWorker(
    IServiceScopeFactory scopes, TimeProvider reloj, ILogger<RankingMusicaWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RetrasoReintento = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan EsperaMaximaTemporizador = TimeSpan.FromDays(30);

    public async Task<DateTimeOffset?> EjecutarCiclo(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRankingMusicaServicio>().ProcesarRonda(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var proximoCierre = await IntentarProcesarRonda(stoppingToken);
            if (proximoCierre is { } cierre)
                await EsperarHastaElCierre(cierre, stoppingToken);
            else
                await Task.Delay(RetrasoReintento, reloj, stoppingToken);
        }
    }

    private async Task EsperarHastaElCierre(DateTimeOffset cierre, CancellationToken ct)
    {
        // Una fecha inicial muy lejana puede superar el límite de un temporizador de .NET.
        var restante = cierre - reloj.GetUtcNow();
        while (restante > TimeSpan.Zero)
        {
            var espera = restante < EsperaMaximaTemporizador ? restante : EsperaMaximaTemporizador;
            await Task.Delay(espera, reloj, ct);
            restante = cierre - reloj.GetUtcNow();
        }
    }

    private async Task<DateTimeOffset?> IntentarProcesarRonda(CancellationToken ct)
    {
        try
        {
            return await EjecutarCiclo(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo procesar la ronda de música. Se reintentará.");
            return null;
        }
    }
}
