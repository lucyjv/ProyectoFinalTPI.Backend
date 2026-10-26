using Microsoft.AspNetCore.SignalR;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ChatTests
{
    // ──────────────────────────────────────────────
    // ENVÍO DE MENSAJES
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Enviar_mensaje_cuando_destinatario_esta_conectado_entrega_mensaje()
    {
        await using var factory = new ChatFactory();

        // Registrar dos usuarios y obtener sus tokens
        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        // Conectar ambos al hub
        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);

        await connA.StartAsync();
        await connB.StartAsync();
        // El handshake del cliente puede terminar antes de OnConnectedAsync.
        // Una invocación confirma que B ya está registrado antes del envío de A.
        await connB.InvokeAsync<List<MensajeChatRespuesta>>("ObtenerHistorial", idA);

        // Preparar receptor: B espera recibir un mensaje
        var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        connB.On<object>("RecibirMensaje", msg => tcs.TrySetResult(msg));

        // A envía un mensaje a B
        await connA.InvokeAsync("EnviarMensaje", idB, "Hola desde A");

        // Esperamos máximo 5 segundos a que B reciba el mensaje
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Enviar_mensaje_cuando_destinatario_esta_conectado_persiste_en_bd()
    {
        await using var factory = new ChatFactory();

        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);

        await connA.StartAsync();
        await connB.StartAsync();

        // Esperamos confirmación de envío antes de consultar la BD
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connA.On<object>("MensajeEnviado", _ => tcs.TrySetResult(true));

        await connA.InvokeAsync("EnviarMensaje", idB, "Mensaje persistente");

        await Task.WhenAny(tcs.Task, Task.Delay(5000));

        // Verificar que el mensaje quedó en la base de datos
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mensajes = await db.MensajesChat
            .Where(m => m.EmisorId == idA && m.DestinatarioId == idB)
            .ToListAsync();

        Assert.Single(mensajes);
        Assert.Equal("Mensaje persistente", mensajes[0].Contenido);
    }

    [Fact]
    public async Task Enviar_mensaje_a_usuario_offline_persiste_en_bd()
    {
        await using var factory = new ChatFactory();

        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (_, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        // Solo A se conecta; B nunca se conecta al hub (está offline)
        await using var connA = CrearConexion(factory, tokenA);
        await connA.StartAsync();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connA.On<object>("MensajeEnviado", _ => tcs.TrySetResult(true));

        // Enviar a B offline ya no lanza excepción, sino que se procesa correctamente
        await connA.InvokeAsync("EnviarMensaje", idB, "Mensaje para usuario offline");

        await Task.WhenAny(tcs.Task, Task.Delay(5000));

        // Verificar que el mensaje quedó guardado en la base de datos
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mensajes = await db.MensajesChat
            .Where(m => m.EmisorId == idA && m.DestinatarioId == idB)
            .ToListAsync();

        Assert.Single(mensajes);
        Assert.Equal("Mensaje para usuario offline", mensajes[0].Contenido);
    }

    [Fact]
    public async Task Enviar_mensaje_vacio_lanza_hub_exception()
    {
        await using var factory = new ChatFactory();

        var (tokenA, _) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);
        await connA.StartAsync();
        await connB.StartAsync();

        await Assert.ThrowsAsync<HubException>(
            () => connA.InvokeAsync("EnviarMensaje", idB, ""));
    }

    [Fact]
    public async Task Enviar_mensaje_a_uno_mismo_lanza_hub_exception()
    {
        await using var factory = new ChatFactory();

        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await connA.StartAsync();

        await Assert.ThrowsAsync<HubException>(
            () => connA.InvokeAsync("EnviarMensaje", idA, "Mensaje a mí mismo"));
    }

    [Fact]
    public async Task Mensaje_supera_limite_de_caracteres_lanza_hub_exception()
    {
        await using var factory = new ChatFactory();

        var (tokenA, _) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);
        await connA.StartAsync();
        await connB.StartAsync();

        var mensajeLargo = new string('x', 2001);

        await Assert.ThrowsAsync<HubException>(
            () => connA.InvokeAsync("EnviarMensaje", idB, mensajeLargo));
    }

    // ──────────────────────────────────────────────
    // HISTORIAL
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Obtener_historial_devuelve_mensajes_en_orden_cronologico()
    {
        await using var factory = new ChatFactory();

        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);
        await connA.StartAsync();
        await connB.StartAsync();

        // Enviar tres mensajes y esperar confirmación de cada uno
        // para garantizar orden de inserción en BD
        foreach (var texto in new[] { "Primero", "Segundo", "Tercero" })
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            connA.On<object>("MensajeEnviado", _ => tcs.TrySetResult(true));
            await connA.InvokeAsync("EnviarMensaje", idB, texto);
            await Task.WhenAny(tcs.Task, Task.Delay(3000));
            connA.Remove("MensajeEnviado");
        }

        // Pedir historial
        var historial = await connA.InvokeAsync<List<MensajeChatRespuesta>>(
            "ObtenerHistorial", idB);

        Assert.Equal(3, historial.Count);
        Assert.Equal("Primero", historial[0].Contenido);
        Assert.Equal("Segundo", historial[1].Contenido);
        Assert.Equal("Tercero", historial[2].Contenido);
    }

    [Fact]
    public async Task Obtener_historial_vacio_devuelve_lista_vacia()
    {
        await using var factory = new ChatFactory();

        var (tokenA, _)   = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await connA.StartAsync();

        var historial = await connA.InvokeAsync<List<MensajeChatRespuesta>>(
            "ObtenerHistorial", idB);

        Assert.Empty(historial);
    }

    // ──────────────────────────────────────────────
    // PRESENCIA
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Conectarse_al_hub_sin_token_es_rechazado()
    {
        await using var factory = new ChatFactory();

        // Crear conexión sin token
        await using var conn = CrearConexion(factory, token: null);

        // La conexión debe ser rechazada (401)
        await Assert.ThrowsAsync<HttpRequestException>(
            () => conn.StartAsync());
    }

    [Fact]
    public async Task Desconectarse_permite_que_mensajes_posteriores_se_persistan()
    {
        await using var factory = new ChatFactory();

        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);
        await connA.StartAsync();
        await connB.StartAsync();

        // B se desconecta
        await connB.StopAsync();

        // Pequeña espera para que el servidor procese la desconexión
        await Task.Delay(200);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connA.On<object>("MensajeEnviado", _ => tcs.TrySetResult(true));

        // A le envía un mensaje a B, que ahora está offline. Debe tener éxito y guardarse.
        await connA.InvokeAsync("EnviarMensaje", idB, "B ya no está pero el mensaje se guarda");

        await Task.WhenAny(tcs.Task, Task.Delay(5000));

        // Verificar que se persistió en BD a pesar de estar offline
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mensajes = await db.MensajesChat
            .Where(m => m.EmisorId == idA && m.DestinatarioId == idB)
            .ToListAsync();

        Assert.Single(mensajes);
        Assert.Equal("B ya no está pero el mensaje se guarda", mensajes[0].Contenido);
    }

    // ──────────────────────────────────────────────
    // Infraestructura de test
    // ──────────────────────────────────────────────

    /// <summary>
    /// Registra un usuario vía HTTP y devuelve su token JWT y su Id.
    /// </summary>
    private static async Task<(string token, int id)> RegistrarYObtenerToken(
        ChatFactory factory, string username, string email)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = username,
            Email = email,
            Contraseña = "contraseña123"
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthRespuesta>();
        return (body!.Token, body.UsuarioId);
    }

    /// <summary>
    /// Crea un HubConnection apuntando al TestServer interno de la factory.
    /// El token viaja por query string porque SignalR no soporta headers
    /// personalizados en WebSockets.
    /// </summary>
    private static HubConnection CrearConexion(ChatFactory factory, string? token)
    {
        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/chat", options =>
            {
                // Reutilizar el HttpMessageHandler del TestServer en lugar de
                // hacer conexiones de red reales. Fundamental para tests.
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                if (token is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    private sealed class ChatFactory
        : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Clave"]     = "clave-de-testing-no-usar-en-produccion-32ch",
                    ["Jwt:Emisor"]    = "nostalgia-backend",
                    ["Jwt:Audiencia"] = "nostalgia-frontend"
                });
            });
            builder.ConfigureServices(services =>
            {
                // Reemplazar PostgreSQL por base de datos en memoria
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                var nombre = Guid.NewGuid().ToString();
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(nombre));
            });
        }
    }

    private record AuthRespuesta(string Token, int UsuarioId, string Username, string Email, bool EsAdmin);

    // DTO de respuesta del historial — coincide con MensajeChatDto del servidor
    private record MensajeChatRespuesta(
        int Id,
        int EmisorId,
        string EmisorUsername,
        int DestinatarioId,
        string Contenido,
        DateTime FechaEnvio);
}
