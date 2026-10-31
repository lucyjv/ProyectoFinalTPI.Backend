using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using ProyectoFinalTPI.Backend.Services;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class GooglePhotosTests
{
    #region OAuth y PKCE

    [Fact]
    public async Task QueOAuthUsePkceYSesionUnicaSinExponerTokens()
    {
        //Arrange
        using var f = new Fixture();
        var query = QueryHelpers.ParseQuery(new Uri(f.Start.AuthorizationUrl).Query);

        Assert.Equal(GooglePhotosService.Scope, query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.DoesNotContain("test-secret", f.Start.AuthorizationUrl);

        f.Responses.Enqueue(request =>
        {
            var body = QueryHelpers.ParseQuery(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(body["code_verifier"].ToString())));
            Assert.Equal(query["code_challenge"].ToString(), challenge);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"private-token\"}") };
        });

        f.Responses.Enqueue(request =>
        {
            using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            Assert.Equal("1", body.RootElement.GetProperty("pickingConfig").GetProperty("maxItemCount").GetString());
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"session\",\"pickerUri\":\"https://photos.google.com/picker/session\",\"mediaItemsSet\":true}") };
        });

        //Act
        await f.Service.Callback(f.State, "browser", "code", null, default);
        var status = await f.Service.Status(f.Start.OperationId, 1, default);

        //Assert
        Assert.Equal("ready", status.Status);
        Assert.DoesNotContain("private-token", JsonSerializer.Serialize(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("another-browser")]
    public async Task QueCallbackRequieraNavegadorOriginal(string? binding)
    {
        //Arrange
        using var f = new Fixture();

        //Act
        var ex = await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, binding, "code", null, default));

        //Assert
        Assert.Equal(400, ex.Status);
        Assert.Empty(f.Urls);
    }

    [Fact]
    public async Task QueEstadoInvalidoYReplayDeCallbackSeanRechazados()
    {
        //Arrange
        using var f = new Fixture();

        //Act & Assert - Estado inválido
        Assert.Equal(400, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback("invalid", "browser", "code", null, default))).Status);

        //Act & Assert - Replay
        await f.Authorize();
        Assert.Equal(400, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, "browser", "code", null, default))).Status);
        Assert.Equal(2, f.Urls.Count);
        Assert.Equal("ready", (await f.Service.Status(f.Start.OperationId, 1, default)).Status);
    }

    [Fact]
    public async Task QueOtroUsuarioNoPuedaLeerImportarNiCancelarOperacion()
    {
        //Arrange
        using var f = new Fixture();

        //Act & Assert
        Assert.Equal(404, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Status(f.Start.OperationId, 2, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 2, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Cancel(f.Start.OperationId, 2, default))).Status);
        Assert.Empty(f.Urls);
    }

    [Fact]
    public async Task QueOperacionExpiradaNoPuedaSerUtilizada()
    {
        //Arrange
        using var f = new Fixture();
        f.Clock.Now += TimeSpan.FromMinutes(16);

        //Act & Assert
        Assert.Equal(410, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Status(f.Start.OperationId, 1, default))).Status);
        Assert.Equal(410, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, "browser", "code", null, default))).Status);
        Assert.Empty(f.Urls);
    }

    #endregion

    #region Polling e Importación

    [Fact]
    public async Task QuePollingRespeteIntervaloDeGoogle()
    {
        //Arrange
        using var f = new Fixture();
        await f.Authorize(false);

        //Act & Assert - Dentro del intervalo no vuelve a consultar a Google
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal("selecting", (await f.Service.Status(f.Start.OperationId, 1, default)).Status);
        }
        Assert.Equal(2, f.Urls.Count);

        //Act & Assert - Al pasar el intervalo vuelve a consultar
        f.Clock.Now += TimeSpan.FromSeconds(8);
        f.Json("{\"mediaItemsSet\":true}");
        Assert.Equal("ready", (await f.Service.Status(f.Start.OperationId, 1, default)).Status);
        Assert.Equal(3, f.Urls.Count);
    }

    [Fact]
    public async Task QueImportacionDevuelvaArchivoLimpieSesionRemotaYSeaReintentable()
    {
        //Arrange
        using var f = new Fixture();
        await f.Authorize();
        f.Selection();
        f.Photo();
        f.Cleanup();

        //Act
        var photo = await f.Service.Import(f.Start.OperationId, 1, default);

        //Assert
        Assert.Equal("image/jpeg", photo.ContentType);
        Assert.Equal("google-photos.jpg", photo.FileName);
        Assert.Same(photo, await f.Service.Import(f.Start.OperationId, 1, default));
        Assert.Equal("imported", (await f.Service.Status(f.Start.OperationId, 1, default)).Status);
        Assert.Empty(f.Responses);

        await f.Service.Cancel(f.Start.OperationId, 1, default);
        Assert.Equal(404, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Status(f.Start.OperationId, 1, default))).Status);
    }

    [Theory]
    [InlineData("http://lh3.googleusercontent.com/photo")]
    [InlineData("https://googleusercontent.com.evil.example/photo")]
    [InlineData("https://127.0.0.1/private")]
    public async Task QueDescargaRechaceOrigenNoConfiable(string url)
    {
        //Arrange
        using var f = new Fixture();
        await f.Authorize();
        f.Selection(url);

        //Act & Assert
        Assert.Equal(502, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 1, default))).Status);
        Assert.Equal(3, f.Urls.Count);
    }

    [Fact]
    public async Task QueVideoSeaRechazadoAntesDeLaDescarga()
    {
        //Arrange
        using var f = new Fixture();
        await f.Authorize();
        f.Selection(type: "VIDEO");

        //Act & Assert
        Assert.Equal(415, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 1, default))).Status);
    }

    [Fact]
    public async Task QueImagenExcesivaIntenteTamanioMenorYDevuelva413()
    {
        //Arrange
        using var f = new Fixture();
        await f.Authorize();
        f.Selection();
        for (var i = 0; i < 3; i++) f.Photo(new byte[GooglePhotosService.MaxBytes + 1]);

        //Act & Assert
        Assert.Equal(413, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 1, default))).Status);
        Assert.Contains(f.Urls, u => u.EndsWith("=w1024-h1024"));
    }

    [Fact]
    public async Task QueFirmaDeImagenInvalidaSeaRechazada()
    {
        //Arrange
        using var f = new Fixture();
        await f.Authorize();
        f.Selection();
        f.Photo(Encoding.UTF8.GetBytes("not an image"));

        //Act & Assert
        Assert.Equal(415, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 1, default))).Status);
    }

    #endregion

    #region Cancelación y Configuración

    [Fact]
    public async Task QueConsentimientoDenegadoSeReporteYCancelarPermitaReiniciar()
    {
        //Arrange
        using var f = new Fixture();

        //Act & Assert
        Assert.Equal(400, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, "browser", null, "access_denied", default))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Status(f.Start.OperationId, 1, default))).Status);

        await f.Service.Cancel(f.Start.OperationId, 1, default);
        Assert.NotEqual(f.Start.OperationId, f.Service.Start(1, "browser").OperationId);
    }

    [Fact]
    public async Task QueCancelarElimineSesionRemotaEImpidaImportacion()
    {
        //Arrange
        using var f = new Fixture();
        await f.Authorize();
        f.Cleanup();

        //Act
        await f.Service.Cancel(f.Start.OperationId, 1, default);

        //Assert
        Assert.Equal(404, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 1, default))).Status);
        Assert.Empty(f.Responses);
    }

    [Fact]
    public void QueFaltaDeConfiguracionDevuelva503SinAfectarInicio()
    {
        //Arrange
        var httpFactoryMock = new Mock<IHttpClientFactory>();
        using var service = new GooglePhotosService(httpFactoryMock.Object, new ConfigurationBuilder().Build(), TimeProvider.System, NullLogger<GooglePhotosService>.Instance);

        //Act & Assert
        Assert.Equal(503, Assert.Throws<GooglePhotosException>(() => service.Start(1, "browser")).Status);
    }

    [Theory]
    [InlineData("SERVICE_DISABLED", 503, "no está habilitada")]
    [InlineData("ACCESS_TOKEN_SCOPE_INSUFFICIENT", 409, "Falta el permiso")]
    [InlineData("UNKNOWN_PRIVATE_REASON", 403, "Google rechazó el acceso")]
    public async Task QueErroresDelPickerSeanClasificadosYPreservados(string reason, int status, string message)
    {
        //Arrange
        using var f = new Fixture();
        f.Json("{\"access_token\":\"private-token\"}");
        f.Responses.Enqueue(_ => new(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { error = new { message = "private-token secret payload", details = new[] { new { reason } } } }))
        });

        //Act
        var error = await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, "browser", "code", null, default));

        //Assert
        Assert.Equal(status, error.Status);
        Assert.Contains(message, error.Message);
        Assert.DoesNotContain("private-token", error.Message);
        var frontend = await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Status(f.Start.OperationId, 1, default));
        Assert.Equal(error.Message, frontend.Message);
    }

    [Fact]
    public async Task QueFaltaDeScopeConcedidoSeDetecteAntesDeCrearPicker()
    {
        //Arrange
        using var f = new Fixture();
        f.Json("{\"access_token\":\"private-token\",\"scope\":\"openid email\"}");

        //Act
        var error = await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, "browser", "code", null, default));

        //Assert
        Assert.Contains("no concedió", error.Message);
        Assert.Single(f.Urls);
    }

    #endregion

    #region Fixture Auxiliar

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture : IDisposable
    {
        public readonly Clock Clock = new();
        public readonly Mock<IHttpClientFactory> HttpFactoryMock = new();
        public readonly Mock<HttpMessageHandler> HttpHandlerMock = new();
        public readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> Responses = new();
        public readonly List<string> Urls = new();
        public readonly GooglePhotosService Service;
        public readonly GooglePhotosStart Start;

        public string State => QueryHelpers.ParseQuery(new Uri(Start.AuthorizationUrl).Query)["state"].ToString();

        public Fixture()
        {
            HttpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns((HttpRequestMessage request, CancellationToken _) =>
                {
                    Urls.Add(request.RequestUri!.AbsoluteUri);
                    Assert.NotEmpty(Responses);
                    return Task.FromResult(Responses.Dequeue()(request));
                });

            HttpFactoryMock
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(HttpHandlerMock.Object, disposeHandler: false));

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GooglePhotos:ClientId"] = "test-client",
                ["GooglePhotos:ClientSecret"] = "test-secret",
                ["GooglePhotos:RedirectUri"] = "http://localhost:5000/api/integraciones/google-photos/callback"
            }).Build();

            Service = new(HttpFactoryMock.Object, config, Clock, NullLogger<GooglePhotosService>.Instance);
            Start = Service.Start(1, "browser");
        }

        public void Json(string json) =>
            Responses.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent(json) });

        public async Task Authorize(bool ready = true)
        {
            Json("{\"access_token\":\"private-token\",\"expires_in\":3600}");
            Json("{\"id\":\"session\",\"pickerUri\":\"https://photos.google.com/picker/session\",\"mediaItemsSet\":" +
                (ready ? "true" : "false") + ",\"pollingConfig\":{\"pollInterval\":\"7s\",\"timeoutIn\":\"600s\"}}");
            Assert.EndsWith("/autoclose", await Service.Callback(State, "browser", "code", null, default));
        }

        public void Selection(string url = "https://lh3.googleusercontent.com/photo", string type = "PHOTO") =>
            Json(JsonSerializer.Serialize(new { mediaItems = new[] { new { type, mediaFile = new { baseUrl = url } } } }));

        public void Photo(byte[]? data = null, string mime = "image/jpeg") => Responses.Enqueue(request =>
        {
            Assert.Equal("private-token", request.Headers.Authorization?.Parameter);
            Assert.Contains("=w", request.RequestUri!.AbsoluteUri);
            var content = new ByteArrayContent(data ?? new byte[] { 255, 216, 255, 1, 2, 3 });
            content.Headers.ContentType = new MediaTypeHeaderValue(mime);
            return new(HttpStatusCode.OK) { Content = content };
        });

        public void Cleanup() => Responses.Enqueue(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            return new(HttpStatusCode.NoContent);
        });

        public void Dispose()
        {
            Service.Dispose();
        }
    }

    #endregion
}
