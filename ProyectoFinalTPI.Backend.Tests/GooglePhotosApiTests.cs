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
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Services;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class GooglePhotosApiTests
{
    private const string Key = "google-photos-tests-only-key-at-least-32-characters";
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
                // Program captures its signing key before this factory's configuration callback.
                // Set the test signing key explicitly, retaining real JWT validation.
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
                    options.TokenValidationParameters.ValidIssuer = "photos-test";
                    options.TokenValidationParameters.ValidAudience = "photos-test";
                });
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            });
        }
    }
    private static void Login(HttpClient client, int id)
    {
        var token = new JwtSecurityToken("photos-test", "photos-test", new[] { new Claim("sub", id.ToString()) },
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    [Theory]
    [InlineData("POST", "sesiones")]
    [InlineData("GET", "sesiones/unknown")]
    [InlineData("POST", "sesiones/unknown/imagen")]
    [InlineData("DELETE", "sesiones/unknown")]
    public async Task Endpoints_require_Nostalgiar_authentication(string method, string path)
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/api/integraciones/google-photos/" + path));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Start_sets_private_browser_cookie_and_cannot_be_read_by_another_user()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Login(client, 1);
        using var response = await client.PostAsync("/api/integraciones/google-photos/sesiones", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", cookie.ToLowerInvariant());
        Assert.Contains("secure", cookie.ToLowerInvariant());
        var start = await response.Content.ReadFromJsonAsync<GooglePhotosStart>();
        Assert.NotNull(start);
        Login(client, 2);
        using var other = await client.GetAsync("/api/integraciones/google-photos/sesiones/" + start.OperationId);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Login(client, 1);
        using var cancel = await client.DeleteAsync("/api/integraciones/google-photos/sesiones/" + start.OperationId);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    [Fact]
    public async Task Callback_is_public_but_requires_valid_state_and_browser_binding()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync("/api/integraciones/google-photos/callback?state=invalid&code=test");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }
}
