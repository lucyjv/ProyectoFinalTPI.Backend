using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AnalizadorRecuerdoGeminiTests : IDisposable
{
    private readonly HttpFake _http = new();
    private readonly Mock<ICatalogoAnalisisRepositorio> _catalogo = new();
    private readonly OpcionesGemini _opciones = new() { Habilitado = true, ApiKey = "clave-ficticia-de-test" };
    private readonly ControlCuotaGemini _cuota = new(TimeProvider.System);
    private readonly HttpClient _cliente;
    private readonly AnalizadorRecuerdoGemini _analizador;
    private readonly EntradaAnalisisRecuerdoDto _entrada = new(10, 1, "México 86", "Argentina campeón del mundo 1986", new DateTime(1986, 6, 29));
    private static PropuestaMomentoDto Propuesta(int id = 0, double confianza = 0.95) =>
        new(id, "Argentina campeón del mundo 1986", 1986, ["México 86"], confianza, "Argentina campeón del mundo 1986");

    public AnalizadorRecuerdoGeminiTests()
    {
        _catalogo.Setup(r => r.BuscarCandidatos(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _cliente = new(_http);
        _analizador = new(_cliente, _catalogo.Object, Options.Create(_opciones), _cuota);
    }

    private void Responder(params PropuestaMomentoDto[] propuestas) => _http.Cuerpo = JsonSerializer.Serialize(new
    {
        candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = JsonSerializer.Serialize(new { propuestas }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } } } } }
    });

    [Fact]
    public async Task QueSeEnvieLaClavePorHeaderYElRecuerdoComoDatosSeparadosDeLasInstrucciones()
    {
        //Arrange
        Responder(Propuesta());

        //Act
        var result = await _analizador.Analizar(_entrada, CancellationToken.None);

        //Assert
        Assert.Equal(_opciones.ApiKey, _http.Clave);
        Assert.Equal("generativelanguage.googleapis.com", _http.Url!.Host);
        Assert.Empty(_http.Url.Query);
        Assert.DoesNotContain(_opciones.ApiKey, _http.CuerpoEnviado!);
        using var cuerpo = JsonDocument.Parse(_http.CuerpoEnviado!);
        Assert.True(cuerpo.RootElement.TryGetProperty("systemInstruction", out _));
        Assert.Equal("application/json", cuerpo.RootElement.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
        Assert.Equal(1986, Assert.Single(result.Propuestas!).Anio);
        Assert.Empty(result.Momentos);
    }

    [Fact]
    public async Task QueSePuedaElegirUnMomentoDelCatalogoSinCrearOtro()
    {
        //Arrange
        _catalogo.Setup(r => r.BuscarCandidatos(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CandidatoMomentoDto(25, "Argentina campeón 1986", null, null, ["México 86"])]);
        Responder(Propuesta(25));

        //Act
        var result = await _analizador.Analizar(_entrada, CancellationToken.None);

        //Assert
        Assert.Equal(25, Assert.Single(result.Propuestas!).MomentoId);
        Assert.Contains("Argentina campe", _http.CuerpoEnviado!);
    }

    [Fact]
    public async Task QueUnRecuerdoPersonalPuedaQuedarSinMomentos()
    {
        //Arrange
        Responder();

        //Act
        var result = await _analizador.Analizar(_entrada with { Titulo = "Mi cumpleaños", Descripcion = "Con mi familia" }, CancellationToken.None);

        //Assert
        Assert.True(result.AnalisisRealizado);
        Assert.Empty(result.Propuestas!);
    }

    [Theory]
    [InlineData(0.84, 0)]
    [InlineData(0.85, 1)]
    public async Task QueSeRespeteElUmbralDeConfianzaInclusivo(double confianza, int cantidad)
    {
        //Arrange
        Responder(Propuesta(confianza: confianza));

        //Act
        var result = await _analizador.Analizar(_entrada, CancellationToken.None);

        //Assert
        Assert.Equal(cantidad, result.Propuestas!.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task QueSeRechacenIdsInventadosEvidenciaFalsaYAniosInvalidos(int caso)
    {
        //Arrange
        Responder(caso switch
        {
            0 => Propuesta(99),
            1 => Propuesta() with { Evidencia = "No aparece en este recuerdo" },
            2 => Propuesta() with { Anio = 2022 },
            _ => Propuesta() with { Alias = ["Qatar 2022"] }
        });

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _analizador.Analizar(_entrada, CancellationToken.None));

        //Assert
        Assert.Equal(1, _http.Llamadas);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("no es json")]
    [InlineData("{\"candidates\":[]}")]
    [InlineData("{\"candidates\":[{\"finishReason\":\"MAX_TOKENS\"}]}")]
    public async Task QueUnaRespuestaIncompletaNoSeInterpreteComoAusenciaDeMomentos(string cuerpo)
    {
        //Arrange
        _http.Cuerpo = cuerpo;

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _analizador.Analizar(_entrada, CancellationToken.None));

        //Assert
        Assert.Equal(1, _http.Llamadas);
    }

    [Theory]
    [InlineData(429, "proveedor_cuota", false)]
    [InlineData(403, "proveedor_credenciales", true)]
    [InlineData(404, "proveedor_configuracion", true)]
    [InlineData(503, "proveedor_no_disponible", false)]
    public async Task QueLosErroresDelProveedorSeanAcotadosYSinTextoPrivado(int status, string codigo, bool permanente)
    {
        //Arrange
        _http.Estado = (HttpStatusCode)status;
        _http.Cuerpo = "contenido privado que no debe guardarse";

        //Act
        var result = await Assert.ThrowsAsync<ErrorProveedorAnalisis>(() => _analizador.Analizar(_entrada, CancellationToken.None));

        //Assert
        Assert.Equal(codigo, result.Codigo);
        Assert.Equal(permanente, result.Permanente);
        Assert.DoesNotContain("privado", result.Message);
        if (status == 429) Assert.Equal(TimeSpan.FromSeconds(120), result.ReintentarEn);
    }

    [Fact]
    public async Task QueSePuedaCambiarElModeloPorConfiguracion()
    {
        //Arrange
        _opciones.Modelo = "otro-modelo";
        Responder();

        //Act
        await _analizador.Analizar(_entrada, CancellationToken.None);

        //Assert
        Assert.EndsWith("/otro-modelo:generateContent", _http.Url!.AbsolutePath);
        Assert.Contains("otro-modelo", _analizador.Version);
    }

    [Theory]
    [InlineData("confianza")]
    [InlineData("momentoId")]
    public async Task QueUnCampoObligatorioAusenteNoSeReemplacePorUnValorPredeterminado(string campo)
    {
        //Arrange
        var propuesta = JsonSerializer.SerializeToNode(Propuesta(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        propuesta.Remove(campo);
        _http.Cuerpo = JsonSerializer.Serialize(new { candidates = new[] { new
        {
            finishReason = "STOP", content = new { parts = new[] { new { text = JsonSerializer.Serialize(new { propuestas = new[] { propuesta } }) } } }
        } } });

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _analizador.Analizar(_entrada, CancellationToken.None));

        //Assert
        Assert.Equal(1, _http.Llamadas);
    }

    [Fact]
    public async Task QueUnaRespuestaExcesivaNoSeCargueComoResultadoValido()
    {
        //Arrange
        _http.Cuerpo = new string('x', 65537);

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _analizador.Analizar(_entrada, CancellationToken.None));

        //Assert
        Assert.Equal(1, _http.Llamadas);
    }

    [Fact]
    public async Task QueNoSeLlameAlProveedorSinClaveNiCuandoSeCancelo()
    {
        //Arrange
        _opciones.ApiKey = "";

        //Act
        var error = await Assert.ThrowsAsync<ErrorProveedorAnalisis>(() => _analizador.Analizar(_entrada, CancellationToken.None));
        _opciones.ApiKey = "ficticia";
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _analizador.Analizar(_entrada, new CancellationToken(true)));

        //Assert
        Assert.True(error.Permanente);
        Assert.Equal(0, _http.Llamadas);
    }

    private sealed class HttpFake : HttpMessageHandler
    {
        public string Cuerpo = "{}";
        public string? CuerpoEnviado, Clave;
        public Uri? Url;
        public int Llamadas;
        public HttpStatusCode Estado = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Llamadas++; Url = request.RequestUri;
            Clave = request.Headers.GetValues("x-goog-api-key").Single();
            CuerpoEnviado = await request.Content!.ReadAsStringAsync(ct);
            var respuesta = new HttpResponseMessage(Estado) { Content = new StringContent(Cuerpo, Encoding.UTF8, "application/json") };
            respuesta.Headers.RetryAfter = new(TimeSpan.FromSeconds(120));
            return respuesta;
        }
    }

    public void Dispose() { _cliente.Dispose(); _cuota.Dispose(); }
}
