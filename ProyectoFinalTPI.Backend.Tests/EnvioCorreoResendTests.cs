using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class EnvioCorreoResendTests
{
    private static readonly CorreoElectronicoDto Correo = new("ana@example.com", "Recuperar cuenta",
        "<p>Enlace de prueba</p>", "Enlace de prueba", "recuperacion/prueba");

    [Fact]
    public async Task QueUnReintentoConserveLaClaveIdempotenteYElContenido()
    {
        //Arrange: el handler reemplaza la red, no envía ningún correo real.
        using var handler = new HandlerResend(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        using var cliente = new HttpClient(handler) { BaseAddress = new("https://api.resend.com/") };
        var servicio = CrearServicio(cliente);

        //Act
        await servicio.EnviarAsync(Correo);

        //Assert
        Assert.Equal(2, handler.Solicitudes.Count);
        Assert.All(handler.Solicitudes, s =>
        {
            Assert.Equal("https://api.resend.com/emails", s.Url);
            Assert.Equal("recuperacion/prueba", s.Clave);
            Assert.Equal("Bearer re_solo_para_tests", s.Autorizacion);
            Assert.Equal("Nostalgiar/1.0", s.UserAgent);
        });
        Assert.Equal(handler.Solicitudes[0].Contenido, handler.Solicitudes[1].Contenido);
        using var json = JsonDocument.Parse(handler.Solicitudes[0].Contenido);
        Assert.Equal("ana@example.com", json.RootElement.GetProperty("to")[0].GetString());
        Assert.Equal(Correo.Html, json.RootElement.GetProperty("html").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task QueNoSeReintentenErroresDeConfiguracionOCuota(HttpStatusCode estado)
    {
        //Arrange
        using var handler = new HandlerResend(estado);
        using var cliente = new HttpClient(handler) { BaseAddress = new("https://api.resend.com/") };

        //Act
        var error = await Assert.ThrowsAsync<EnvioCorreoException>(() => CrearServicio(cliente).EnviarAsync(Correo));

        //Assert
        Assert.Single(handler.Solicitudes);
        Assert.Equal($"resend_http_{(int)estado}", error.Codigo);
        Assert.DoesNotContain(Correo.Destinatario, error.Message);
        Assert.DoesNotContain("re_solo_para_tests", error.Message);
    }

    private static EnvioCorreoResend CrearServicio(HttpClient cliente) => new(cliente,
        Options.Create(new CorreoResendOptions { ApiKey = "re_solo_para_tests" }));

    private sealed class HandlerResend(params HttpStatusCode[] estados) : HttpMessageHandler
    {
        public List<(string Url, string Clave, string Autorizacion, string Contenido, string UserAgent)> Solicitudes { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var indice = Solicitudes.Count;
            Solicitudes.Add((request.RequestUri!.ToString(), request.Headers.GetValues("Idempotency-Key").Single(),
                request.Headers.Authorization!.ToString(), await request.Content!.ReadAsStringAsync(cancellationToken),
                request.Headers.UserAgent.ToString()));
            return new(estados[indice]) { Content = new StringContent("{}") };
        }
    }
}
