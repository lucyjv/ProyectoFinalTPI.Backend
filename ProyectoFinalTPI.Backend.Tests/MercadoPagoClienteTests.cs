using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Infraestructura;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class MercadoPagoClienteTests
{
    private static readonly SolicitudPreferenciaMercadoPago Solicitud = new(42, "ficha-retro", "Ficha Retro", 15, 3000m);

    [Theory]
    [InlineData("http://localhost:3001", "https://sandbox.example/pago", false)]
    [InlineData("https://nostalgiar.example/", "https://checkout.example/pago", true)]
    public async Task CrearPreferenciaConservaImporteReferenciaRetornosYSeleccionDeEnlace(
        string retorno, string enlaceEsperado, bool autoRetorno)
    {
        using var handler = new ProveedorSimulado();
        using var http = new HttpClient(handler);
        var cliente = CrearCliente(http, retorno: retorno);

        var resultado = await cliente.CrearPreferenciaAsync(Solicitud);

        Assert.Equal(new PreferenciaMercadoPago("pref-42", enlaceEsperado), resultado);
        Assert.Equal(HttpMethod.Post, handler.Metodo);
        Assert.Equal("https://api.mercadopago.com/checkout/preferences", handler.Url);
        Assert.Equal("Bearer token-prueba", handler.Autorizacion);
        using var json = JsonDocument.Parse(handler.Cuerpo!);
        var raiz = json.RootElement;
        var item = Assert.Single(raiz.GetProperty("items").EnumerateArray());
        Assert.Equal("ficha-retro", item.GetProperty("id").GetString());
        Assert.Equal("Nostalgiar - Ficha Retro (15 Puntos)", item.GetProperty("title").GetString());
        Assert.Equal("ARS", item.GetProperty("currency_id").GetString());
        Assert.Equal(3000m, item.GetProperty("unit_price").GetDecimal());
        Assert.Equal(1, item.GetProperty("quantity").GetInt32());
        Assert.Equal("42", raiz.GetProperty("external_reference").GetString());
        var urls = raiz.GetProperty("back_urls");
        Assert.Equal($"{retorno.TrimEnd('/')}/perfil?puntos=exito&compraId=42", urls.GetProperty("success").GetString());
        Assert.Equal($"{retorno.TrimEnd('/')}/perfil?puntos=fallo&compraId=42", urls.GetProperty("failure").GetString());
        Assert.Equal($"{retorno.TrimEnd('/')}/perfil?puntos=pendiente&compraId=42", urls.GetProperty("pending").GetString());
        Assert.Equal(autoRetorno, raiz.TryGetProperty("auto_return", out var autoReturn));
        if (autoRetorno) Assert.Equal("approved", autoReturn.GetString());
    }

    [Theory]
    [InlineData("http://localhost:3001", "https://checkout.example/pago", null)]
    [InlineData("https://nostalgiar.example", null, "https://sandbox.example/pago")]
    public async Task UsaElEnlaceDisponibleCuandoFaltaElPreferido(string retorno, string? normal, string? sandbox)
    {
        using var handler = new ProveedorSimulado
        {
            Respuesta = new { id = "pref-42", init_point = normal, sandbox_init_point = sandbox }
        };
        using var http = new HttpClient(handler);
        var resultado = await CrearCliente(http, retorno: retorno).CrearPreferenciaAsync(Solicitud);
        Assert.Equal(normal ?? sandbox, resultado.UrlPago);
    }

    [Theory]
    [InlineData("")]
    [InlineData("REEMPLAZAR_TOKEN")]
    public async Task SinCredencialesNoEnviaSolicitudes(string token)
    {
        using var handler = new ProveedorSimulado();
        using var http = new HttpClient(handler);
        var cliente = CrearCliente(http, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cliente.CrearPreferenciaAsync(Solicitud));
        Assert.Null(await cliente.ConsultarPagoAsync("12345"));
        Assert.Null(handler.Url);
    }

    [Fact]
    public async Task ConsultaPagoConAutenticacionYDevuelveEstadoYReferencia()
    {
        using var handler = new ProveedorSimulado { Respuesta = new { status = "approved", external_reference = "42" } };
        using var http = new HttpClient(handler);
        var resultado = await CrearCliente(http).ConsultarPagoAsync("12345");
        Assert.Equal(new PagoMercadoPago("approved", "42"), resultado);
        Assert.Equal(HttpMethod.Get, handler.Metodo);
        Assert.Equal("https://api.mercadopago.com/v1/payments/12345", handler.Url);
        Assert.Equal("Bearer token-prueba", handler.Autorizacion);
    }

    [Fact]
    public async Task ErrorDelProveedorImpideCrearPreferenciaYNoConfirmaPago()
    {
        using var handler = new ProveedorSimulado { EstadoHttp = HttpStatusCode.Unauthorized };
        using var http = new HttpClient(handler);
        var cliente = CrearCliente(http);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cliente.CrearPreferenciaAsync(Solicitud));
        Assert.Null(await cliente.ConsultarPagoAsync("12345"));
    }

    [Fact]
    public async Task PreferenciaSinEnlaceNoPuedeIniciarPago()
    {
        using var handler = new ProveedorSimulado { Respuesta = new { id = "pref-42" } };
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CrearCliente(http).CrearPreferenciaAsync(Solicitud));
    }

    [Fact]
    public void ClienteSeResuelveConLaMismaSeccionDeConfiguracion()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MercadoPago:AccessToken"] = "token-prueba",
            ["MercadoPago:BaseUrlRetorno"] = "https://nostalgiar.example"
        }).Build();
        var servicios = new ServiceCollection();
        servicios.Configure<MercadoPagoOptions>(configuracion.GetSection(MercadoPagoOptions.Seccion));
        servicios.AddHttpClient<IMercadoPagoCliente, MercadoPagoCliente>();
        using var proveedor = servicios.BuildServiceProvider();
        Assert.IsType<MercadoPagoCliente>(proveedor.GetRequiredService<IMercadoPagoCliente>());
        var opciones = proveedor.GetRequiredService<IOptions<MercadoPagoOptions>>().Value;
        Assert.Equal("token-prueba", opciones.AccessToken);
        Assert.Equal("https://nostalgiar.example", opciones.BaseUrlRetorno);
    }

    private static MercadoPagoCliente CrearCliente(HttpClient http, string token = "token-prueba", string retorno = "http://localhost:3001") =>
        new(http, Options.Create(new MercadoPagoOptions { AccessToken = token, BaseUrlRetorno = retorno }));

    private sealed class ProveedorSimulado : HttpMessageHandler
    {
        public object Respuesta { get; init; } = new
        {
            id = "pref-42", init_point = "https://checkout.example/pago", sandbox_init_point = "https://sandbox.example/pago"
        };
        public HttpStatusCode EstadoHttp { get; init; } = HttpStatusCode.OK;
        public HttpMethod? Metodo { get; private set; }
        public string? Url { get; private set; }
        public string? Autorizacion { get; private set; }
        public string? Cuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Metodo = request.Method;
            Url = request.RequestUri!.AbsoluteUri;
            Autorizacion = request.Headers.Authorization?.ToString();
            Cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(EstadoHttp) { Content = JsonContent.Create(Respuesta) };
        }
    }
}
