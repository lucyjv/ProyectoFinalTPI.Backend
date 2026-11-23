namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class MercadoPagoOptions
{
    public const string Seccion = "MercadoPago";
    public string AccessToken { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string BaseUrlRetorno { get; set; } = "http://localhost:3001";
}
