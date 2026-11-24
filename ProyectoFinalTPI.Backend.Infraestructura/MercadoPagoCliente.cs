using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Infraestructura;

public sealed class MercadoPagoCliente(
    HttpClient cliente,
    IOptions<MercadoPagoOptions> opcionesMercadoPago,
    ILogger<MercadoPagoCliente>? logger = null) : IMercadoPagoCliente
{
    public async Task<PreferenciaMercadoPago> CrearPreferenciaAsync(SolicitudPreferenciaMercadoPago solicitud, CancellationToken ct = default)
    {
        var mpConfig = opcionesMercadoPago.Value;
        if (string.IsNullOrWhiteSpace(mpConfig.AccessToken) || mpConfig.AccessToken.StartsWith("REEMPLAZAR", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogError("No se puede iniciar el pago: MercadoPago:AccessToken no está configurado.");
            throw new InvalidOperationException("El servicio de cobro de Mercado Pago no está configurado en el servidor.");
        }

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mpConfig.AccessToken);

        var esHttps = mpConfig.BaseUrlRetorno.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        var payload = new Dictionary<string, object?>
        {
            ["items"] = new[]
            {
                new
                {
                    id = solicitud.PackId,
                    title = $"Nostalgiar - {solicitud.NombrePack} ({solicitud.Puntos} Puntos)",
                    description = $"Recarga de {solicitud.Puntos} puntos de nostalgia para tu cuenta en Nostalgiar.",
                    quantity = 1,
                    currency_id = "ARS",
                    unit_price = solicitud.PrecioArs
                }
            },
            ["back_urls"] = new
            {
                success = $"{mpConfig.BaseUrlRetorno.TrimEnd('/')}/perfil?puntos=exito&compraId={solicitud.CompraId}",
                failure = $"{mpConfig.BaseUrlRetorno.TrimEnd('/')}/perfil?puntos=fallo&compraId={solicitud.CompraId}",
                pending = $"{mpConfig.BaseUrlRetorno.TrimEnd('/')}/perfil?puntos=pendiente&compraId={solicitud.CompraId}"
            },
            ["external_reference"] = solicitud.CompraId.ToString()
        };

        if (esHttps)
        {
            payload["auto_return"] = "approved";
        }

        using var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var respuesta = await cliente.PostAsync("https://api.mercadopago.com/checkout/preferences", jsonContent, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var err = await respuesta.Content.ReadAsStringAsync(ct);
            logger?.LogError("Mercado Pago respondió con error al crear preferencia ({StatusCode}): {Body}", (int)respuesta.StatusCode, err);
            throw new InvalidOperationException("Mercado Pago no pudo generar la preferencia de pago. Verifique las credenciales y la conexión.");
        }

        await using var stream = await respuesta.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        string preferenceId = string.Empty;
        if (doc.RootElement.TryGetProperty("id", out var idProp))
        {
            preferenceId = idProp.GetString() ?? string.Empty;
        }

        string initPoint = string.Empty;
        var esLocalhost = mpConfig.BaseUrlRetorno.Contains("localhost", StringComparison.OrdinalIgnoreCase);

        // Si estamos en entorno local de pruebas y Mercado Pago devolvió sandbox_init_point, lo priorizamos
        if (esLocalhost && doc.RootElement.TryGetProperty("sandbox_init_point", out var sandProp) && !string.IsNullOrWhiteSpace(sandProp.GetString()))
        {
            initPoint = sandProp.GetString()!;
        }
        else if (doc.RootElement.TryGetProperty("init_point", out var initProp) && !string.IsNullOrWhiteSpace(initProp.GetString()))
        {
            initPoint = initProp.GetString()!;
        }
        else if (doc.RootElement.TryGetProperty("sandbox_init_point", out var fallbackSandProp) && !string.IsNullOrWhiteSpace(fallbackSandProp.GetString()))
        {
            initPoint = fallbackSandProp.GetString()!;
        }

        if (string.IsNullOrWhiteSpace(initPoint))
        {
            logger?.LogError("Mercado Pago no devolvió una URL init_point válida.");
            throw new InvalidOperationException("Mercado Pago no devolvió un enlace de pago válido.");
        }

        return new PreferenciaMercadoPago(preferenceId, initPoint);
    }

    public async Task<PagoMercadoPago?> ConsultarPagoAsync(string paymentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paymentId)) return null;

        var mpConfig = opcionesMercadoPago.Value;
        if (string.IsNullOrWhiteSpace(mpConfig.AccessToken) || mpConfig.AccessToken.StartsWith("REEMPLAZAR", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning("No se puede consultar pago {PaymentId} a Mercado Pago porque AccessToken no está configurado.", paymentId);
            return null;
        }

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mpConfig.AccessToken);

        using var respuesta = await cliente.GetAsync($"https://api.mercadopago.com/v1/payments/{paymentId}", ct);
        if (!respuesta.IsSuccessStatusCode)
        {
            logger?.LogWarning("Mercado Pago respondió {StatusCode} al consultar pago {PaymentId}", (int)respuesta.StatusCode, paymentId);
            return null;
        }

        await using var stream = await respuesta.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        var extRef = root.TryGetProperty("external_reference", out var extProp) ? extProp.GetString() : null;

        return new PagoMercadoPago(status, extRef);
    }
}
