using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using StackExchange.Redis;

namespace ProyectoFinalTPI.Backend.Repositorio.Cache;

// El número de generación evita que una lectura concurrente repueble una clave invalidada.
public sealed class CacheLecturasPublicas(IDistributedCache cache, ILogger<CacheLecturasPublicas> logger) : ICacheLecturasPublicas, IInvalidadorLecturasPublicas
{
    private const string Generacion = "publico:generacion:v1";

    private static TimeSpan DuracionPara(string clave) => clave switch
    {
        "mapa:pines:v1" => TimeSpan.FromMinutes(30),
        _ when clave.StartsWith("perfil:", StringComparison.Ordinal) => TimeSpan.FromMinutes(10),
        _ => TimeSpan.FromMinutes(2)
    };
    public async Task<T?> Obtener<T>(string clave, Func<Task<T?>> consultar, CancellationToken ct) where T : class
    {
        string? version;
        try
        {
            version = await cache.GetStringAsync(Generacion, ct);
            if (version is null)
            {
                await InvalidarAsync(ct);
                version = await cache.GetStringAsync(Generacion, ct);
            }
            if (version is null) return await consultar();
            var bytes = await cache.GetAsync($"publico:{version}:{clave}", ct);
            if (bytes is not null)
            {
                try { return JsonSerializer.Deserialize<T>(bytes); }
                catch (JsonException) { /* Una entrada inválida se reconstruye desde la base. */ }
            }
        }
        catch (RedisException)
        {
            TelemetriaRedis.RegistrarFallback();
            logger.LogWarning("Redis no disponible: se consulta la base para {Clave}.", clave);
            return await consultar();
        }
        var valor = await consultar();
        if (valor is null) return null;
        try
        {
            await cache.SetAsync($"publico:{version}:{clave}", JsonSerializer.SerializeToUtf8Bytes(valor),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = DuracionPara(clave) }, ct);
        }
        catch (RedisException) { logger.LogWarning("No se pudo guardar la caché de {Clave}.", clave); }
        return valor;
    }

    public void Invalidar()
    {
        try { cache.SetString(Generacion, Guid.NewGuid().ToString("N")); }
        catch (RedisException) { logger.LogWarning("Falló la invalidación; las entradas públicas conservan su vencimiento configurado (hasta 30 minutos)."); }
    }

    public async Task InvalidarAsync(CancellationToken ct)
    {
        try { await cache.SetStringAsync(Generacion, Guid.NewGuid().ToString("N"), token: ct); }
        catch (RedisException) { logger.LogWarning("Falló la invalidación; las entradas públicas conservan su vencimiento configurado (hasta 30 minutos)."); }
    }
}
