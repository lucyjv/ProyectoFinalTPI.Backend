using System.Diagnostics;
using System.Diagnostics.Metrics;
using StackExchange.Redis;

namespace ProyectoFinalTPI.Backend.Repositorio.Cache;

internal static class TelemetriaRedis
{
    private static readonly ActivitySource Fuente = new("Nostalgiar.Redis");
    private static readonly Meter Medidor = new("Nostalgiar.Redis");
    private static readonly Counter<long> Fallbacks = Medidor.CreateCounter<long>("redis.lecturas.fallback");
    private static readonly Histogram<double> Duracion = Medidor.CreateHistogram<double>("redis.sesion.duracion", "ms");
    public static void RegistrarFallback() => Fallbacks.Add(1);

    public static T Medir<T>(string operacion, Func<T> ejecutar)
    {
        using var actividad = Fuente.StartActivity($"redis.sesion.{operacion}", ActivityKind.Client);
        actividad?.SetTag("db.system", "redis");
        var inicio = Stopwatch.GetTimestamp();
        var resultado = "ok";
        try { return ejecutar(); }
        catch (RedisException)
        {
            resultado = "error";
            actividad?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            Duracion.Record(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds,
                new KeyValuePair<string, object?>("operacion", operacion),
                new KeyValuePair<string, object?>("resultado", resultado));
        }
    }
}
