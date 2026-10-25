using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ProyectoFinalTPI.Backend.Services;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class GooglePhotosTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class FakeHttp : HttpMessageHandler, IHttpClientFactory
    {
        public readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> Responses = new();
        public readonly List<string> Urls = new();
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.AbsoluteUri);
            Assert.NotEmpty(Responses);
            return Task.FromResult(Responses.Dequeue()(request));
        }
        public void Json(string json) => Responses.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
    private sealed class Fixture : IDisposable
    {
        public readonly Clock Clock = new();
        public readonly FakeHttp Http = new();
        public readonly GooglePhotosService Service;
        public readonly GooglePhotosStart Start;
        public string State => QueryHelpers.ParseQuery(new Uri(Start.AuthorizationUrl).Query)["state"].ToString();
        public Fixture()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GooglePhotos:ClientId"] = "test-client", ["GooglePhotos:ClientSecret"] = "test-secret",
                ["GooglePhotos:RedirectUri"] = "http://localhost:5000/api/integraciones/google-photos/callback"
            }).Build();
            Service = new(Http, config, Clock, NullLogger<GooglePhotosService>.Instance);
            Start = Service.Start(1, "browser");
        }
        public async Task Authorize(bool ready = true)
        {
            Http.Json("{\"access_token\":\"private-token\",\"expires_in\":3600}");
            Http.Json("{\"id\":\"session\",\"pickerUri\":\"https://photos.google.com/picker/session\",\"mediaItemsSet\":" +
                (ready ? "true" : "false") + ",\"pollingConfig\":{\"pollInterval\":\"7s\",\"timeoutIn\":\"600s\"}}");
            Assert.EndsWith("/autoclose", await Service.Callback(State, "browser", "code", null, default));
        }
        public void Selection(string url = "https://lh3.googleusercontent.com/photo", string type = "PHOTO") =>
            Http.Json(JsonSerializer.Serialize(new { mediaItems = new[] { new { type, mediaFile = new { baseUrl = url } } } }));
        public void Photo(byte[]? data = null, string mime = "image/jpeg") => Http.Responses.Enqueue(request =>
        {
            Assert.Equal("private-token", request.Headers.Authorization?.Parameter);
            Assert.Contains("=w", request.RequestUri!.AbsoluteUri);
            var content = new ByteArrayContent(data ?? new byte[] { 255, 216, 255, 1, 2, 3 });
            content.Headers.ContentType = new MediaTypeHeaderValue(mime);
            return new(HttpStatusCode.OK) { Content = content };
        });
        public void Cleanup() => Http.Responses.Enqueue(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            return new(HttpStatusCode.NoContent);
        });
        public void Dispose() { Service.Dispose(); Http.Dispose(); }
    }

    [Fact]
    public async Task OAuth_uses_PKCE_and_single_photo_session_without_exposing_tokens()
    {
        using var f = new Fixture();
        var query = QueryHelpers.ParseQuery(new Uri(f.Start.AuthorizationUrl).Query);
        Assert.Equal(GooglePhotosService.Scope, query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.DoesNotContain("test-secret", f.Start.AuthorizationUrl);
        f.Http.Responses.Enqueue(request =>
        {
            var body = QueryHelpers.ParseQuery(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(body["code_verifier"].ToString())));
            Assert.Equal(query["code_challenge"].ToString(), challenge);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"private-token\"}") };
        });
        f.Http.Responses.Enqueue(request =>
        {
            using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            Assert.Equal("1", body.RootElement.GetProperty("pickingConfig").GetProperty("maxItemCount").GetString());
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"session\",\"pickerUri\":\"https://photos.google.com/picker/session\",\"mediaItemsSet\":true}") };
        });
        await f.Service.Callback(f.State, "browser", "code", null, default);
        var status = await f.Service.Status(f.Start.OperationId, 1, default);
        Assert.Equal("ready", status.Status);
        Assert.DoesNotContain("private-token", JsonSerializer.Serialize(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("another-browser")]
    public async Task Callback_requires_original_browser(string? binding)
    {
        using var f = new Fixture();
        var ex = await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, binding, "code", null, default));
        Assert.Equal(400, ex.Status);
        Assert.Empty(f.Http.Urls);
    }

    [Fact]
    public async Task Invalid_state_and_callback_replay_are_rejected()
    {
        using var f = new Fixture();
        Assert.Equal(400, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback("invalid", "browser", "code", null, default))).Status);
        await f.Authorize();
        Assert.Equal(400, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, "browser", "code", null, default))).Status);
        Assert.Equal(2, f.Http.Urls.Count);
        Assert.Equal("ready", (await f.Service.Status(f.Start.OperationId, 1, default)).Status);
    }

    [Fact]
    public async Task Other_user_cannot_read_import_or_cancel_operation()
    {
        using var f = new Fixture();
        Assert.Equal(404, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Status(f.Start.OperationId, 2, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 2, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Cancel(f.Start.OperationId, 2, default))).Status);
        Assert.Empty(f.Http.Urls);
    }

    [Fact]
    public async Task Expired_operation_cannot_be_used()
    {
        using var f = new Fixture();
        f.Clock.Now += TimeSpan.FromMinutes(16);
        Assert.Equal(410, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Status(f.Start.OperationId, 1, default))).Status);
        Assert.Equal(410, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, "browser", "code", null, default))).Status);
        Assert.Empty(f.Http.Urls);
    }

    [Fact]
    public async Task Polling_respects_Google_interval()
    {
        using var f = new Fixture();
        await f.Authorize(false);
        for (var i = 0; i < 4; i++) Assert.Equal("selecting", (await f.Service.Status(f.Start.OperationId, 1, default)).Status);
        Assert.Equal(2, f.Http.Urls.Count);
        f.Clock.Now += TimeSpan.FromSeconds(8);
        f.Http.Json("{\"mediaItemsSet\":true}");
        Assert.Equal("ready", (await f.Service.Status(f.Start.OperationId, 1, default)).Status);
        Assert.Equal(3, f.Http.Urls.Count);
    }

    [Fact]
    public async Task Import_returns_file_cleans_remote_session_and_is_retryable()
    {
        using var f = new Fixture();
        await f.Authorize(); f.Selection(); f.Photo(); f.Cleanup();
        var photo = await f.Service.Import(f.Start.OperationId, 1, default);
        Assert.Equal("image/jpeg", photo.ContentType);
        Assert.Equal("google-photos.jpg", photo.FileName);
        Assert.Same(photo, await f.Service.Import(f.Start.OperationId, 1, default));
        Assert.Equal("imported", (await f.Service.Status(f.Start.OperationId, 1, default)).Status);
        Assert.Empty(f.Http.Responses);
        await f.Service.Cancel(f.Start.OperationId, 1, default);
        Assert.Equal(404, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Status(f.Start.OperationId, 1, default))).Status);
    }

    [Theory]
    [InlineData("http://lh3.googleusercontent.com/photo")]
    [InlineData("https://googleusercontent.com.evil.example/photo")]
    [InlineData("https://127.0.0.1/private")]
    public async Task Download_rejects_untrusted_source(string url)
    {
        using var f = new Fixture();
        await f.Authorize(); f.Selection(url);
        Assert.Equal(502, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 1, default))).Status);
        Assert.Equal(3, f.Http.Urls.Count);
    }

    [Fact]
    public async Task Video_is_rejected_before_download()
    {
        using var f = new Fixture();
        await f.Authorize(); f.Selection(type: "VIDEO");
        Assert.Equal(415, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 1, default))).Status);
    }

    [Fact]
    public async Task Oversized_image_tries_smaller_renditions_then_returns_413()
    {
        using var f = new Fixture();
        await f.Authorize(); f.Selection();
        for (var i = 0; i < 3; i++) f.Photo(new byte[GooglePhotosService.MaxBytes + 1]);
        Assert.Equal(413, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 1, default))).Status);
        Assert.Contains(f.Http.Urls, u => u.EndsWith("=w1024-h1024"));
    }

    [Fact]
    public async Task Invalid_image_signature_is_rejected()
    {
        using var f = new Fixture();
        await f.Authorize(); f.Selection(); f.Photo(Encoding.UTF8.GetBytes("not an image"));
        Assert.Equal(415, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 1, default))).Status);
    }

    [Fact]
    public async Task Denied_consent_is_reported_and_cancel_allows_restart()
    {
        using var f = new Fixture();
        Assert.Equal(400, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, "browser", null, "access_denied", default))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Status(f.Start.OperationId, 1, default))).Status);
        await f.Service.Cancel(f.Start.OperationId, 1, default);
        Assert.NotEqual(f.Start.OperationId, f.Service.Start(1, "browser").OperationId);
    }

    [Fact]
    public async Task Cancel_removes_remote_session_and_prevents_import()
    {
        using var f = new Fixture();
        await f.Authorize(); f.Cleanup();
        await f.Service.Cancel(f.Start.OperationId, 1, default);
        Assert.Equal(404, (await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Import(f.Start.OperationId, 1, default))).Status);
        Assert.Empty(f.Http.Responses);
    }

    [Fact]
    public void Missing_configuration_returns_503_without_affecting_application_startup()
    {
        using var http = new FakeHttp();
        using var service = new GooglePhotosService(http, new ConfigurationBuilder().Build(), TimeProvider.System, NullLogger<GooglePhotosService>.Instance);
        Assert.Equal(503, Assert.Throws<GooglePhotosException>(() => service.Start(1, "browser")).Status);
    }

    [Theory]
    [InlineData("SERVICE_DISABLED", 503, "no está habilitada")]
    [InlineData("ACCESS_TOKEN_SCOPE_INSUFFICIENT", 409, "Falta el permiso")]
    [InlineData("UNKNOWN_PRIVATE_REASON", 403, "Google rechazó el acceso")]
    public async Task Picker_errors_are_classified_and_preserved_for_frontend(string reason, int status, string message)
    {
        using var f = new Fixture();
        f.Http.Json("{\"access_token\":\"private-token\"}");
        f.Http.Responses.Enqueue(_ => new(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { error = new { message = "private-token secret payload", details = new[] { new { reason } } } }))
        });
        var error = await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, "browser", "code", null, default));
        Assert.Equal(status, error.Status);
        Assert.Contains(message, error.Message);
        Assert.DoesNotContain("private-token", error.Message);
        var frontend = await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Status(f.Start.OperationId, 1, default));
        Assert.Equal(error.Message, frontend.Message);
    }

    [Fact]
    public async Task Missing_granted_scope_is_detected_before_creating_picker()
    {
        using var f = new Fixture();
        f.Http.Json("{\"access_token\":\"private-token\",\"scope\":\"openid email\"}");
        var error = await Assert.ThrowsAsync<GooglePhotosException>(() => f.Service.Callback(f.State, "browser", "code", null, default));
        Assert.Contains("no concedió", error.Message);
        Assert.Single(f.Http.Urls);
    }
}
