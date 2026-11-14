using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class EnvioCorreoResend(HttpClient cliente, IOptions<CorreoResendOptions> opciones) : IEnvioCorreo
{
    public async Task EnviarAsync(CorreoElectronicoDto correo, CancellationToken cancellationToken = default)
    {
        var configuracion = opciones.Value;
        for (var intento = 1; intento <= 3; intento++)
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "emails");
            solicitud.Headers.UserAgent.ParseAdd("Nostalgiar/1.0");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuracion.ApiKey);
            solicitud.Headers.Add("Idempotency-Key", correo.ClaveIdempotencia);
            solicitud.Content = JsonContent.Create(new
            {
                from = new MailAddress(configuracion.Remitente, configuracion.NombreRemitente).ToString(),
                to = new[] { correo.Destinatario }, subject = correo.Asunto, html = correo.Html, text = correo.Texto
            });
            try
            {
                using var respuesta = await cliente.SendAsync(solicitud, cancellationToken);
                if (respuesta.IsSuccessStatusCode) return;
                var transitorio = (int)respuesta.StatusCode >= 500 || respuesta.StatusCode == HttpStatusCode.RequestTimeout;
                if (!transitorio || intento == 3)
                    throw new EnvioCorreoException($"resend_http_{(int)respuesta.StatusCode}");
            }
            catch (HttpRequestException) when (intento < 3) { }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && intento < 3) { }
            await Task.Delay(TimeSpan.FromSeconds(intento), cancellationToken);
        }
    }
}

// El código permite diagnosticar el fallo sin registrar destinatarios, claves, enlaces ni respuestas del proveedor
public sealed class EnvioCorreoException(string codigo) : Exception("No se pudo enviar el correo.")
{
    public string Codigo { get; } = codigo;
}
