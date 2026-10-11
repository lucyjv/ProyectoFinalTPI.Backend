using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AuthTests
{
    // ──────────────────────────────────────────────
    // REGISTRO
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Registro_exitoso_devuelve_201_con_token_valido()
    {
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthRespuesta>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.Token));
        Assert.Equal("testuser", body.Username);
        Assert.Equal("test@example.com", body.Email);
        Assert.False(body.EsAdmin);
        Assert.True(body.UsuarioId > 0);

        // Verificamos que el token sea un JWT bien formado
        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(body.Token));
        var jwt = handler.ReadJwtToken(body.Token);
        Assert.Equal("nostalgia-backend", jwt.Issuer);
    }

    [Fact]
    public async Task Registro_hashea_la_contraseña_en_la_base_de_datos()
    {
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Buscamos por email para no confundirnos con el usuario seed del Program.cs
        var usuario = await db.Usuarios.FirstAsync(u => u.Email == "test@example.com");

        // La contraseña en la BD nunca debe ser el texto plano
        Assert.NotEqual("contraseña123", usuario.Contraseña);
        // Y debe ser un hash BCrypt válido (empieza con $2a$ o $2b$)
        Assert.Matches(@"^\$2[ab]\$", usuario.Contraseña);
    }

    [Fact]
    public async Task Registro_con_email_duplicado_devuelve_409()
    {
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        var payload = new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        };

        await client.PostAsJsonAsync("/api/auth/registro", payload);
        var response = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "otrouser",
            Email = "test@example.com", // mismo email
            Contraseña = "otracontraseña123"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("email", await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", "test@example.com", "contraseña123")]        // username vacío
    [InlineData("ab", "test@example.com", "contraseña123")]      // username muy corto
    [InlineData("user", "no-es-email", "contraseña123")]         // email inválido
    [InlineData("user", "", "contraseña123")]                    // email vacío
    [InlineData("user", "test@example.com", "corta")]            // contraseña muy corta
    [InlineData("user", "test@example.com", "")]                 // contraseña vacía
    public async Task Registro_con_datos_invalidos_devuelve_400(
        string username, string email, string contraseña)
    {
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = username,
            Email = email,
            Contraseña = contraseña
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verificamos que no se registró ningún usuario nuevo (puede existir el seed)
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Usuarios.Where(u => u.Email == "test@example.com").ToListAsync());
    }

    // ──────────────────────────────────────────────
    // LOGIN
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Login_exitoso_devuelve_200_con_token_valido()
    {
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        // Primero registramos
        await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        // Luego hacemos login
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthRespuesta>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.Token));
        Assert.Equal("testuser", body.Username);
    }

    [Fact]
    public async Task Login_con_contraseña_incorrecta_devuelve_401()
    {
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "test@example.com",
            Contraseña = "contraseña_incorrecta"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_con_email_inexistente_devuelve_401()
    {
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "noexiste@example.com",
            Contraseña = "contraseña123"
        });

        // Devuelve 401 (no 404) para no revelar si el email existe en el sistema
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "contraseña123")]       // email vacío
    [InlineData("no-es-email", "contraseña123")] // email inválido
    [InlineData("test@example.com", "")]    // contraseña vacía
    public async Task Login_con_datos_invalidos_devuelve_400(string email, string contraseña)
    {
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Contraseña = contraseña
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Infraestructura de test (mismo patrón que CrearPublicacionTests)
    // ──────────────────────────────────────────────

    private sealed class AuthFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                // Proveer configuración JWT para el entorno de tests
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Clave"]      = "clave-de-testing-no-usar-en-produccion-32ch",
                    ["Jwt:Emisor"]     = "nostalgia-backend",
                    ["Jwt:Audiencia"]  = "nostalgia-frontend"
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
}
