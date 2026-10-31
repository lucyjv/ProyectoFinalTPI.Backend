using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Services;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class GooglePhotosApiTests
{
    private static readonly Guid ActiveSession = Guid.ParseExact("1b2c3d4e5f6041728394a5b6c7d8e9f0", "N");
    private const string Key = "google-photos-tests-only-key-at-least-32-characters";

    #region Autenticación y Autorización de Endpoints

    [Theory]
    [InlineData("POST", "sesiones")]
    [InlineData("GET", "sesiones/unknown")]
    [InlineData("POST", "sesiones/unknown/imagen")]
    [InlineData("DELETE", "sesiones/unknown")]
    public async Task QueEndpointsRequieranAutenticacion(string method, string path)
    {
        //Arrange
        await using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        //Act
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/api/integraciones/google-photos/" + path));

        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Flujo de Sesión y Cookies

    [Fact]
    public async Task QueInicioConfigureCookiePrivadaYNoPuedaSerLeidaPorOtroUsuario()
    {
        //Arrange
        await using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Login(client, 1);

        //Act - Inicio de sesión
        using var response = await client.PostAsync("/api/integraciones/google-photos/sesiones", null);

        //Assert - Validar cookie segura
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", cookie.ToLowerInvariant());
        Assert.Contains("secure", cookie.ToLowerInvariant());

        var start = await response.Content.ReadFromJsonAsync<GooglePhotosStart>();
        Assert.NotNull(start);

        //Act & Assert - Otro usuario no puede consultar la sesión
        Login(client, 2);
        using var other = await client.GetAsync("/api/integraciones/google-photos/sesiones/" + start.OperationId);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);

        //Act & Assert - Usuario propietario puede cancelar
        Login(client, 1);
        using var cancel = await client.DeleteAsync("/api/integraciones/google-photos/sesiones/" + start.OperationId);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    [Fact]
    public async Task QueCallbackRequieraEstadoValidoYVinculacionConNavegador()
    {
        //Arrange
        await using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        //Act
        using var response = await client.GetAsync("/api/integraciones/google-photos/callback?state=invalid&code=test");

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    #endregion

    #region Infraestructura y Helpers de Test

    private static void Login(HttpClient client, int id)
    {
        var token = new JwtSecurityToken("photos-test", "photos-test", new[] { new Claim("sub", id.ToString()), new Claim("sid", ActiveSession.ToString("N")) },
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Clave"] = Key, ["Jwt:Emisor"] = "photos-test", ["Jwt:Audiencia"] = "photos-test",
                ["GooglePhotos:ClientId"] = "fake-client", ["GooglePhotos:ClientSecret"] = "fake-secret",
                ["GooglePhotos:RedirectUri"] = "http://localhost:5000/api/integraciones/google-photos/callback"
            }));
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
                    options.TokenValidationParameters.ValidIssuer = "photos-test";
                    options.TokenValidationParameters.ValidAudience = "photos-test";
                });
                var sessions = new Mock<ISesionUsuarioRepositorio>();
                sessions.Setup(repo => repo.EstaActivaAsync(ActiveSession, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                services.RemoveAll<ISesionUsuarioRepositorio>();
                services.AddSingleton(sessions.Object);
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            });
        }
    }

    #endregion
}
