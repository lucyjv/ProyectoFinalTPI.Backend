using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace ProyectoFinalTPI.Backend.Services;

public sealed class GooglePhotosException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed record GooglePhotosStart(string OperationId, string AuthorizationUrl, DateTimeOffset ExpiresAt);
public sealed record GooglePhotosStatus(string Status, string? PickerUri, int PollAfterSeconds, DateTimeOffset ExpiresAt);
public sealed record GooglePhotosImage(byte[] Bytes, string ContentType, string FileName);

// Operations are deliberately short-lived and local to one server process.
public sealed class GooglePhotosService(IHttpClientFactory clients, IConfiguration configuration,
    TimeProvider clock, ILogger<GooglePhotosService> logger) : BackgroundService
{
    public const string Scope = "https://www.googleapis.com/auth/photospicker.mediaitems.readonly";
    public const int MaxBytes = 2 * 1024 * 1024;
    private const string Api = "https://photospicker.googleapis.com/v1/";
    private readonly ConcurrentDictionary<string, Operation> operations = new();
    private readonly object startLock = new();

    private sealed class Operation(int owner, string binding, string state, string verifier, DateTimeOffset expiry)
    {
        public readonly int Owner = owner;
        public readonly string Binding = binding, State = state, Verifier = verifier;
        public readonly SemaphoreSlim Gate = new(1, 1);
        public DateTimeOffset Expiry = expiry, NextPoll;
        public bool CallbackUsed, Ready, Closed;
        public string? Token, SessionId, PickerUri, Failure;
        public int PollSeconds = 5;
        public GooglePhotosImage? Image;
    }

    private string Setting(string name) => !string.IsNullOrWhiteSpace(configuration[$"GooglePhotos:{name}"])
        ? configuration[$"GooglePhotos:{name}"]!
        : throw new GooglePhotosException(503, "Google Photos no está configurado en el servidor.");
    private static string Random() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public GooglePhotosStart Start(int owner, string binding)
    {
        var clientId = Setting("ClientId");
        _ = Setting("ClientSecret");
        var redirect = Setting("RedirectUri");
        if (!Uri.TryCreate(redirect, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new GooglePhotosException(503, "El callback de Google Photos no es válido.");

        lock (startLock)
        {
            if (operations.Values.Any(o => o.Owner == owner && !o.Closed && o.Expiry > clock.GetUtcNow()))
                throw new GooglePhotosException(409, "Ya hay una selección en curso. Cancelala antes de iniciar otra.");
            if (operations.Count >= 100)
                throw new GooglePhotosException(429, "Hay demasiadas selecciones activas. Intentá más tarde.");
            var id = Random();
            var op = new Operation(owner, binding, Random(), Random(), clock.GetUtcNow().AddMinutes(15));
            operations[id] = op;
            var url = QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?>
            {
                ["client_id"] = clientId, ["redirect_uri"] = redirect, ["response_type"] = "code",
                ["scope"] = Scope, ["state"] = op.State, ["access_type"] = "online",
                ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(op.Verifier))),
                ["code_challenge_method"] = "S256"
            });
            return new(id, url, op.Expiry);
        }
    }

    private Operation Owned(string id, int owner)
    {
        if (!operations.TryGetValue(id, out var op) || op.Owner != owner)
            throw new GooglePhotosException(404, "Selección no encontrada.");
        Check(op);
        return op;
    }

    private void Check(Operation op)
    {
        if (op.Closed || op.Expiry <= clock.GetUtcNow())
            throw new GooglePhotosException(410, "La selección venció. Volvé a conectar Google Photos.");
        if (op.Failure != null) throw new GooglePhotosException(409, op.Failure);
    }

    public async Task<string> Callback(string state, string? binding, string? code, string? error, CancellationToken ct)
    {
        var op = operations.Values.FirstOrDefault(o => o.State == state);
        if (op == null || string.IsNullOrEmpty(binding) || op.Binding != binding)
            throw new GooglePhotosException(400, "La autorización no corresponde a este navegador o venció.");
        await op.Gate.WaitAsync(ct);
        var processingCallback = false;
        try
        {
            Check(op);
            if (op.CallbackUsed) throw new GooglePhotosException(400, "Esta autorización ya fue utilizada.");
            op.CallbackUsed = true;
            processingCallback = true;
            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
                throw new GooglePhotosException(400, "No se autorizó el acceso a Google Photos.");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = Setting("ClientId"), ["client_secret"] = Setting("ClientSecret"),
                    ["redirect_uri"] = Setting("RedirectUri"), ["code"] = code,
                    ["grant_type"] = "authorization_code", ["code_verifier"] = op.Verifier
                })
            };
            using var token = await Json(request, ct);
            if (token.RootElement.TryGetProperty("scope", out var granted) &&
                !(granted.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(Scope))
                throw new GooglePhotosException(409, "Google no concedió el permiso para seleccionar fotos. Volvé a conectar y aceptá el acceso a Google Photos en la pantalla de permisos.");
            op.Token = token.RootElement.GetProperty("access_token").GetString()
                ?? throw new GooglePhotosException(502, "Google no devolvió una autorización válida.");
            if (token.RootElement.TryGetProperty("expires_in", out var expires))
                op.Expiry = Min(op.Expiry, clock.GetUtcNow().AddSeconds(expires.GetInt32()));
            using var create = Authorized(HttpMethod.Post, Api + "sessions", op);
            create.Content = JsonContent.Create(new { pickingConfig = new { maxItemCount = "1" } });
            using var session = await Json(create, ct);
            op.SessionId = session.RootElement.GetProperty("id").GetString();
            op.PickerUri = session.RootElement.GetProperty("pickerUri").GetString();
            if (!Uri.TryCreate(op.PickerUri, UriKind.Absolute, out var picker) || picker.Scheme != "https" || picker.Host != "photos.google.com")
                throw new GooglePhotosException(502, "Google devolvió un selector inválido.");
            Update(op, session.RootElement);
            return op.PickerUri!.TrimEnd('/') + "/autoclose";
        }
        catch (Exception ex) when (ex is GooglePhotosException or HttpRequestException or JsonException or OperationCanceledException or KeyNotFoundException or InvalidOperationException)
        {
            if (processingCallback)
                op.Failure = ex is GooglePhotosException known ? known.Message
                    : "No se pudo conectar Google Photos. Cancelá la selección y volvé a intentar.";
            throw;
        }
        finally { op.Gate.Release(); }
    }

    public async Task<GooglePhotosStatus> Status(string id, int owner, CancellationToken ct)
    {
        var op = Owned(id, owner);
        await op.Gate.WaitAsync(ct);
        try
        {
            Check(op);
            if (op.Image != null) return new("imported", null, 0, op.Expiry);
            if (op.SessionId == null) return new("authorizing", null, 3, op.Expiry);
            if (!op.Ready && clock.GetUtcNow() >= op.NextPoll)
            {
                // Failed requests must also respect the polling interval.
                op.NextPoll = clock.GetUtcNow().AddSeconds(op.PollSeconds);
                using var request = Authorized(HttpMethod.Get, Api + "sessions/" + Uri.EscapeDataString(op.SessionId), op);
                using var session = await Json(request, ct);
                Update(op, session.RootElement);
                Check(op);
            }
            return new(op.Ready ? "ready" : "selecting", op.PickerUri, op.PollSeconds, op.Expiry);
        }
        finally { op.Gate.Release(); }
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    private void Update(Operation op, JsonElement session)
    {
        op.Ready = session.TryGetProperty("mediaItemsSet", out var ready) && ready.GetBoolean();
        if (session.TryGetProperty("expireTime", out var expiry) && expiry.TryGetDateTimeOffset(out var time))
            op.Expiry = Min(op.Expiry, time);
        if (!op.Ready && session.TryGetProperty("pollingConfig", out var polling))
        {
            if (polling.TryGetProperty("pollInterval", out var interval))
                op.PollSeconds = (int)Math.Clamp(Math.Ceiling(Duration(interval)), 1, 3600);
            if (polling.TryGetProperty("timeoutIn", out var timeout))
                op.Expiry = Min(op.Expiry, clock.GetUtcNow().AddSeconds(Duration(timeout)));
        }
        op.NextPoll = clock.GetUtcNow().AddSeconds(op.PollSeconds);
    }
    private static double Duration(JsonElement value) => double.TryParse(value.GetString()?.TrimEnd('s'),
        NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds) && seconds >= 0 ? seconds : 5;

    public async Task<GooglePhotosImage> Import(string id, int owner, CancellationToken ct)
    {
        var op = Owned(id, owner);
        await op.Gate.WaitAsync(ct);
        try
        {
            Check(op);
            if (op.Image != null) return op.Image;
            if (!op.Ready) throw new GooglePhotosException(409, "Todavía no terminaste de seleccionar la foto.");
            using var request = Authorized(HttpMethod.Get, Api + "mediaItems?sessionId=" + Uri.EscapeDataString(op.SessionId!), op);
            using var selected = await Json(request, ct);
            if (!selected.RootElement.TryGetProperty("mediaItems", out var items) || items.GetArrayLength() != 1 ||
                selected.RootElement.TryGetProperty("nextPageToken", out var page) && !string.IsNullOrEmpty(page.GetString()))
                throw new GooglePhotosException(400, "Seleccioná exactamente una foto para el recuerdo.");
            var item = items[0];
            if (item.GetProperty("type").GetString() != "PHOTO")
                throw new GooglePhotosException(415, "Esta integración admite fotos. Seleccioná una imagen.");
            var baseUrl = item.GetProperty("mediaFile").GetProperty("baseUrl").GetString();
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var source) || source.Scheme != "https" ||
                !source.IsDefaultPort || !string.IsNullOrEmpty(source.UserInfo) ||
                !(source.Host == "googleusercontent.com" || source.Host.EndsWith(".googleusercontent.com", StringComparison.OrdinalIgnoreCase)))
                throw new GooglePhotosException(502, "La dirección de la imagen de Google no es válida.");

            // Request a scaled rendition without cropping. Retry smaller sizes if it exceeds the form's 2 MB limit.
            foreach (var dimension in new[] { 2048, 1600, 1024 })
            {
                using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                downloadTimeout.CancelAfter(TimeSpan.FromSeconds(30));
                var downloadToken = downloadTimeout.Token;
                using var download = Authorized(HttpMethod.Get, baseUrl + $"=w{dimension}-h{dimension}", op);
                using var response = await clients.CreateClient("GooglePhotos").SendAsync(download, HttpCompletionOption.ResponseHeadersRead, downloadToken);
                Ensure(response);
                var mime = response.Content.Headers.ContentType?.MediaType;
                if (mime is not ("image/jpeg" or "image/png" or "image/webp" or "image/gif"))
                    throw new GooglePhotosException(415, "Google devolvió un formato de imagen no compatible.");
                if (response.Content.Headers.ContentLength > MaxBytes) continue;
                await using var stream = await response.Content.ReadAsStreamAsync(downloadToken);
                using var output = new MemoryStream();
                var buffer = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(buffer, downloadToken)) > 0)
                {
                    if (output.Length + read > MaxBytes) break;
                    output.Write(buffer, 0, read);
                }
                if (read != 0) continue;
                var bytes = output.ToArray();
                if (!ValidImage(bytes, mime)) throw new GooglePhotosException(415, "El archivo recibido no es una imagen válida.");
                var ext = mime switch { "image/jpeg" => "jpg", "image/png" => "png", "image/webp" => "webp", _ => "gif" };
                op.Image = new(bytes, mime, "google-photos." + ext);
                await Cleanup(op, ct);
                return op.Image;
            }
            throw new GooglePhotosException(413, "La foto supera los 2 MB incluso al reducirla. Elegí otra imagen.");
        }
        finally { op.Gate.Release(); }
    }

    private static bool ValidImage(byte[] data, string mime) => mime switch
    {
        "image/jpeg" => data.Length >= 3 && data[0] == 255 && data[1] == 216 && data[2] == 255,
        "image/png" => data.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/gif" => data.AsSpan().StartsWith("GIF87a"u8) || data.AsSpan().StartsWith("GIF89a"u8),
        "image/webp" => data.Length >= 12 && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    public async Task Cancel(string id, int owner, CancellationToken ct)
    {
        if (!operations.TryGetValue(id, out var op) || op.Owner != owner)
            throw new GooglePhotosException(404, "Selección no encontrada.");
        await Close(id, op, ct);
    }

    private async Task Close(string id, Operation op, CancellationToken ct)
    {
        await op.Gate.WaitAsync(ct);
        try
        {
            op.Closed = true;
            await Cleanup(op, ct);
            op.Image = null;
            operations.TryRemove(id, out _);
        }
        finally { op.Gate.Release(); }
    }

    private async Task Cleanup(Operation op, CancellationToken ct)
    {
        if (op.SessionId != null && op.Token != null)
        {
            try
            {
                using var request = Authorized(HttpMethod.Delete, Api + "sessions/" + Uri.EscapeDataString(op.SessionId), op);
                using var response = await clients.CreateClient("GooglePhotos").SendAsync(request, ct);
                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    logger.LogWarning("No se pudo limpiar una sesión de Google Photos. Estado {Status}", (int)response.StatusCode);
                    return; // Retry on cancel or expiry, retaining credentials only until operation removal.
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                logger.LogWarning("No se pudo contactar Google Photos para limpiar una sesión.");
                return;
            }
        }
        op.Token = null;
        op.SessionId = null;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string uri, Operation op)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", op.Token);
        return request;
    }

    private async Task<JsonDocument> Json(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await clients.CreateClient("GooglePhotos").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Only classify known machine codes; never return or log raw OAuth payloads.
            var body = await response.Content.ReadAsStringAsync(ct);
            var failure = ClassifyError(response.StatusCode, body);
            logger.LogWarning("Google Photos rechazó {Step}: HTTP {Status}, categoría {Category}",
                request.RequestUri?.Host == "oauth2.googleapis.com" ? "OAuth" : "Picker",
                (int)response.StatusCode, failure.Category);
            if (failure.Detail != null) throw new GooglePhotosException(failure.Status, failure.Detail);
        }
        Ensure(response);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private static (string Category, int Status, string? Detail) ClassifyError(System.Net.HttpStatusCode status, string body)
    {
        string? code = null;
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String) code = error.GetString();
                else if (error.ValueKind == JsonValueKind.Object)
                {
                    if (error.TryGetProperty("status", out var state)) code = state.GetString();
                    if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                        foreach (var detail in details.EnumerateArray())
                            if (detail.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String)
                            { code = reason.GetString(); break; }
                }
            }
        }
        catch (JsonException) { }
        return code switch
        {
            "SERVICE_DISABLED" or "accessNotConfigured" => ("api_disabled", 503,
                "Google Photos Picker API no está habilitada para el proyecto del cliente OAuth. Habilitala en Google Cloud, en el mismo proyecto donde creaste las credenciales, y volvé a intentar."),
            "ACCESS_TOKEN_SCOPE_INSUFFICIENT" or "insufficient_scope" => ("missing_scope", 409,
                "Falta el permiso para seleccionar fotos. Volvé a conectar y aceptá el acceso a Google Photos en la pantalla de permisos. Si persiste, revisá el permiso photospicker.mediaitems.readonly en Google Auth Platform."),
            "invalid_client" or "unauthorized_client" => ("invalid_client", 503,
                "Google rechazó las credenciales OAuth del backend. Revisá que ClientId y ClientSecret correspondan al mismo cliente web y que el secreto siga vigente."),
            "invalid_grant" => ("invalid_grant", 409,
                "El código de autorización de Google venció o ya fue utilizado. Cerrá esta ventana y comenzá una nueva selección."),
            "redirect_uri_mismatch" => ("callback_mismatch", 503,
                "La dirección de retorno del backend no coincide con la registrada en el cliente OAuth de Google."),
            "FAILED_PRECONDITION" => ("photos_unavailable", 409,
                "Google Photos no está disponible para esta cuenta. Abrí photos.google.com, comprobá que su biblioteca esté activa y volvé a intentar."),
            _ when status == System.Net.HttpStatusCode.Forbidden => ("forbidden", 403,
                "Google rechazó el acceso al selector de fotos. Revisá que Photos Picker API esté habilitada en el proyecto del cliente OAuth, que la cuenta esté autorizada para probar la aplicación y que hayas aceptado el permiso de fotos."),
            _ => ("unclassified", (int)status, null)
        };
    }

    private static void Ensure(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new GooglePhotosException((int)response.StatusCode switch { 401 or 403 => 409, 429 => 429, _ => 502 },
            (int)response.StatusCode switch
            {
                401 or 403 => "El acceso a Google Photos venció o fue rechazado. Volvé a conectar.",
                429 => "Google Photos recibió demasiadas solicitudes. Intentá más tarde.",
                _ => "Google Photos no pudo completar la operación. Volvé a intentar."
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                foreach (var entry in operations.Where(e => e.Value.Expiry <= clock.GetUtcNow()).ToArray())
                    await Close(entry.Key, entry.Value, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
