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
    #region Envío y Recepción de Mensajes

    [Fact]
    public async Task QueElEnvioDeMensajeConDestinatarioConectadoEntregueElMensaje()
    {
        //Arrange
        await using var factory = new ChatFactory();
        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);

        await connA.StartAsync();
        await connB.StartAsync();
        await connB.InvokeAsync<List<MensajeChatRespuesta>>("ObtenerHistorial", idA);

        var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        connB.On<object>("RecibirMensaje", msg => tcs.TrySetResult(msg));

        //Act
        await connA.InvokeAsync("EnviarMensaje", idB, "Hola desde A");

        //Assert
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task QueElEnvioDeMensajeConDestinatarioConectadoPersistaEnBaseDeDatos()
    {
        //Arrange
        await using var factory = new ChatFactory();
        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);

        await connA.StartAsync();
        await connB.StartAsync();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connA.On<object>("MensajeEnviado", _ => tcs.TrySetResult(true));

        //Act
        await connA.InvokeAsync("EnviarMensaje", idB, "Mensaje persistente");
        await Task.WhenAny(tcs.Task, Task.Delay(5000));

        //Assert
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mensajes = await db.MensajesChat
            .Where(m => m.EmisorId == idA && m.DestinatarioId == idB)
            .ToListAsync();

        Assert.Single(mensajes);
        Assert.Equal("Mensaje persistente", mensajes[0].Contenido);
    }

    [Fact]
    public async Task QueElEnvioDeMensajeAUsuarioOfflinePersistaEnBaseDeDatos()
    {
        //Arrange
        await using var factory = new ChatFactory();
        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (_, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await connA.StartAsync();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connA.On<object>("MensajeEnviado", _ => tcs.TrySetResult(true));

        //Act
        await connA.InvokeAsync("EnviarMensaje", idB, "Mensaje para usuario offline");
        await Task.WhenAny(tcs.Task, Task.Delay(5000));

        //Assert
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mensajes = await db.MensajesChat
            .Where(m => m.EmisorId == idA && m.DestinatarioId == idB)
            .ToListAsync();

        Assert.Single(mensajes);
        Assert.Equal("Mensaje para usuario offline", mensajes[0].Contenido);
    }

    #endregion

    #region Validaciones de Envío

    [Fact]
    public async Task QueFalleElEnvioDeMensajeVacio()
    {
        //Arrange
        await using var factory = new ChatFactory();
        var (tokenA, _) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);
        await connA.StartAsync();
        await connB.StartAsync();

        //Act & Assert
        await Assert.ThrowsAsync<HubException>(
            () => connA.InvokeAsync("EnviarMensaje", idB, ""));
    }

    [Fact]
    public async Task QueFalleElEnvioDeMensajeAUnoMismo()
    {
        //Arrange
        await using var factory = new ChatFactory();
        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await connA.StartAsync();

        //Act & Assert
        await Assert.ThrowsAsync<HubException>(
            () => connA.InvokeAsync("EnviarMensaje", idA, "Mensaje a mí mismo"));
    }

    [Fact]
    public async Task QueFalleElEnvioCuandoElMensajeSuperaLimiteDeCaracteres()
    {
        //Arrange
        await using var factory = new ChatFactory();
        var (tokenA, _) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);
        await connA.StartAsync();
        await connB.StartAsync();

        var mensajeLargo = new string('x', 2001);

        //Act & Assert
        await Assert.ThrowsAsync<HubException>(
            () => connA.InvokeAsync("EnviarMensaje", idB, mensajeLargo));
    }

    #endregion

    #region Historial de Mensajes

    [Fact]
    public async Task QueSeObtengaHistorialEnOrdenCronologico()
    {
        //Arrange
        await using var factory = new ChatFactory();
        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);
        await connA.StartAsync();
        await connB.StartAsync();

        foreach (var texto in new[] { "Primero", "Segundo", "Tercero" })
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            connA.On<object>("MensajeEnviado", _ => tcs.TrySetResult(true));
            await connA.InvokeAsync("EnviarMensaje", idB, texto);
            await Task.WhenAny(tcs.Task, Task.Delay(3000));
            connA.Remove("MensajeEnviado");
        }

        //Act
        var historial = await connA.InvokeAsync<List<MensajeChatRespuesta>>(
            "ObtenerHistorial", idB);

        //Assert
        Assert.Equal(3, historial.Count);
        Assert.Equal("Primero", historial[0].Contenido);
        Assert.Equal("Segundo", historial[1].Contenido);
        Assert.Equal("Tercero", historial[2].Contenido);
    }

    [Fact]
    public async Task QueSeObtengaHistorialVacioCuandoNoHayMensajes()
    {
        //Arrange
        await using var factory = new ChatFactory();
        var (tokenA, _)   = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await connA.StartAsync();

        //Act
        var historial = await connA.InvokeAsync<List<MensajeChatRespuesta>>(
            "ObtenerHistorial", idB);

        //Assert
        Assert.Empty(historial);
    }

    #endregion

    #region Conexión y Presencia

    [Fact]
    public async Task QueFalleLaConexionAlHubSinToken()
    {
        //Arrange
        await using var factory = new ChatFactory();
        await using var conn = CrearConexion(factory, token: null);

        //Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => conn.StartAsync());
    }

    [Fact]
    public async Task QueLaDesconexionPermitaPersistirMensajesPosteriores()
    {
        //Arrange
        await using var factory = new ChatFactory();
        var (tokenA, idA) = await RegistrarYObtenerToken(factory, "usuarioA", "a@test.com");
        var (tokenB, idB) = await RegistrarYObtenerToken(factory, "usuarioB", "b@test.com");

        await using var connA = CrearConexion(factory, tokenA);
        await using var connB = CrearConexion(factory, tokenB);
        await connA.StartAsync();
        await connB.StartAsync();

        await connB.StopAsync();
        await Task.Delay(200);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connA.On<object>("MensajeEnviado", _ => tcs.TrySetResult(true));

        //Act
        await connA.InvokeAsync("EnviarMensaje", idB, "B ya no está pero el mensaje se guarda");
        await Task.WhenAny(tcs.Task, Task.Delay(5000));

        //Assert
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mensajes = await db.MensajesChat
            .Where(m => m.EmisorId == idA && m.DestinatarioId == idB)
            .ToListAsync();

        Assert.Single(mensajes);
        Assert.Equal("B ya no está pero el mensaje se guarda", mensajes[0].Contenido);
    }

    #endregion

    #region Infraestructura y Helpers de Test

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

    private static HubConnection CrearConexion(ChatFactory factory, string? token)
    {
        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/chat", options =>
            {
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
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                var nombre = Guid.NewGuid().ToString();
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(nombre));
            });
        }
    }

    private record AuthRespuesta(string Token, int UsuarioId, string Username, string Email, bool EsAdmin);

    private record MensajeChatRespuesta(
        int Id,
        int EmisorId,
        string EmisorUsername,
        int DestinatarioId,
        string Contenido,
        DateTime FechaEnvio);

    #endregion
}
