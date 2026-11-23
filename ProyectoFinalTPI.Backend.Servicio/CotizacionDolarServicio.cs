using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class CotizacionDolarServicio(
    IHttpClientFactory httpClientFactory,
    IOptions<EconomiaOptions> opciones,
    IDistributedCache? cacheDistribuida = null,
    ILogger<CotizacionDolarServicio>? logger = null) : ICotizacionDolarServicio
{
    private const string ClaveCache = "nostalgiar:economia:cotizacion_dolar";
    private static decimal? _ultimaCotizacionValida;
    private static DateTimeOffset _vencimientoMemoria = DateTimeOffset.MinValue;
    private static readonly SemaphoreSlim _semaforo = new(1, 1);

    public async Task<decimal> ObtenerCotizacionAsync(CancellationToken ct = default)
    {
        var config = opciones.Value;
        var fallback = config.CotizacionDolarFallback > 0 ? config.CotizacionDolarFallback : 1200m;

        // 1. Verificar caché en memoria
        if (_ultimaCotizacionValida.HasValue && DateTimeOffset.UtcNow < _vencimientoMemoria)
        {
            return Math.Max(_ultimaCotizacionValida.Value, fallback);
        }

        // 2. Verificar caché distribuida (Redis)
        if (cacheDistribuida is not null)
        {
            try
            {
                var valorCached = await cacheDistribuida.GetStringAsync(ClaveCache, ct);
                if (!string.IsNullOrEmpty(valorCached) && decimal.TryParse(valorCached, NumberStyles.Any, CultureInfo.InvariantCulture, out var cotizacionCache))
                {
                    _ultimaCotizacionValida = cotizacionCache;
                    _vencimientoMemoria = DateTimeOffset.UtcNow.AddMinutes(Math.Max(5, config.CacheMinutos));
                    return Math.Max(cotizacionCache, fallback);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error al consultar la cotización del dólar en la caché distribuida.");
            }
        }

        await _semaforo.WaitAsync(ct);
        try
        {
            // Doble comprobación tras adquirir el semáforo
            if (_ultimaCotizacionValida.HasValue && DateTimeOffset.UtcNow < _vencimientoMemoria)
            {
                return Math.Max(_ultimaCotizacionValida.Value, fallback);
            }

            // 3. Consultar API pública en vivo
            var cliente = httpClientFactory.CreateClient();
            cliente.Timeout = TimeSpan.FromSeconds(4);

            using var respuesta = await cliente.GetAsync(config.DolarApiUrl, ct);
            if (respuesta.IsSuccessStatusCode)
            {
                await using var stream = await respuesta.Content.ReadAsStreamAsync(ct);
                using var jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                if (jsonDoc.RootElement.TryGetProperty("venta", out var ventaProp) && ventaProp.TryGetDecimal(out var cotizacionApi) && cotizacionApi > 0)
                {
                    var cotizacionFinal = Math.Max(cotizacionApi, fallback);
                    _ultimaCotizacionValida = cotizacionFinal;
                    _vencimientoMemoria = DateTimeOffset.UtcNow.AddMinutes(Math.Max(5, config.CacheMinutos));

                    if (cacheDistribuida is not null)
                    {
                        try
                        {
                            var cacheOpciones = new DistributedCacheEntryOptions
                            {
                                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(5, config.CacheMinutos))
                            };
                            await cacheDistribuida.SetStringAsync(ClaveCache, cotizacionFinal.ToString(CultureInfo.InvariantCulture), cacheOpciones, ct);
                        }
                        catch (Exception exCache)
                        {
                            logger?.LogWarning(exCache, "No se pudo persistir la cotización en Redis.");
                        }
                    }

                    logger?.LogInformation("Cotización del dólar actualizada exitosamente desde API: ${Cotizacion}", cotizacionFinal);
                    return cotizacionFinal;
                }
            }
            else
            {
                logger?.LogWarning("La API del dólar respondió con código de estado {StatusCode}.", (int)respuesta.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Falla al consultar la API del dólar en vivo. Se aplicará cotización de respaldo.");
        }
        finally
        {
            _semaforo.Release();
        }

        // 4. Retorno seguro: última conocida o valor de fallback
        if (_ultimaCotizacionValida.HasValue)
        {
            return Math.Max(_ultimaCotizacionValida.Value, fallback);
        }

        return fallback;
    }
}
