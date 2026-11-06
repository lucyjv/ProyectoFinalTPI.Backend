using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

// evita generar dos rankings al mismo tiempo para el mismo usuario
public sealed class CoordinadorRankingFeed(IServiceScopeFactory scopes,
    IOptions<FeedRecomendacionesOptions> opciones, IHostApplicationLifetime lifetime) : IProveedorRankingFeed
{
    private readonly ConcurrentDictionary<int, Lazy<Task<IReadOnlyList<int>>>> pendientes = new();

    public async Task<IReadOnlyList<int>> ObtenerAsync(int usuarioId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var trabajo = pendientes.GetOrAdd(usuarioId, CrearTrabajo);
        return await trabajo.Value.WaitAsync(cancellationToken);
    }

    private Lazy<Task<IReadOnlyList<int>>> CrearTrabajo(int usuarioId)
    {
        Lazy<Task<IReadOnlyList<int>>> trabajo = null!;
        trabajo = new(() =>
        {
            var ejecucion = GenerarAsync(usuarioId, trabajo);
            // Si todos los clientes cancelan, tenemos un eventual fallo 
            _ = ejecucion.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return ejecucion;
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        return trabajo;
    }

    private async Task<IReadOnlyList<int>> GenerarAsync(int usuarioId, Lazy<Task<IReadOnlyList<int>>> trabajo)
    {
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
            limite.CancelAfter(TimeSpan.FromSeconds(opciones.Value.TiempoMaximoGeneracionSegundos));
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IGeneradorRankingFeed>().GenerarAsync(usuarioId, limite.Token);
        }
        finally
        {
            ((ICollection<KeyValuePair<int, Lazy<Task<IReadOnlyList<int>>>>>)pendientes)
                .Remove(new(usuarioId, trabajo));
        }
    }
}
