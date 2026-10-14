using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

// ──────────────────────────────────────────────────────────────────────────────
// Tests UNITARIOS — prueban AuthServicio directamente con dependencias mockeadas
// ──────────────────────────────────────────────────────────────────────────────
public class AuthServicioTests
{
    private readonly Mock<IUsuarioRepositorio> _repositorioMock = new();
    private readonly IConfiguration _configuracion;

    public AuthServicioTests()
    {
        _configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Clave"]     = "clave-de-testing-no-usar-en-produccion-32ch",
                ["Jwt:Emisor"]    = "nostalgia-backend",
                ["Jwt:Audiencia"] = "nostalgia-frontend"
            })
            .Build();
    }

    private AuthServicio CrearServicio() => new(_repositorioMock.Object, _configuracion);

    [Fact]
    public async Task Registro_hashea_la_contraseña_antes_de_persistir()
    {
        _repositorioMock
            .Setup(r => r.ExistePorEmailAsync("test@example.com", default))
            .ReturnsAsync(false);

        Usuario? usuarioGuardado = null;
        _repositorioMock
            .Setup(r => r.AgregarAsync(It.IsAny<Usuario>(), default))
            .Callback<Usuario, CancellationToken>((u, _) => usuarioGuardado = u)
            .Returns(Task.CompletedTask);

        await CrearServicio().RegistrarAsync(new RegistroDto
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        Assert.NotNull(usuarioGuardado);
        Assert.NotEqual("contraseña123", usuarioGuardado.Contraseña);
        Assert.Matches(@"^\$2[ab]\$", usuarioGuardado.Contraseña);
    }

    [Fact]
    public async Task Registro_con_email_duplicado_lanza_excepcion()
    {
        _repositorioMock
            .Setup(r => r.ExistePorEmailAsync("duplicado@example.com", default))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio().RegistrarAsync(new RegistroDto
            {
                Username = "testuser",
                Email = "duplicado@example.com",
                Contraseña = "contraseña123"
            }));

        _repositorioMock.Verify(r => r.AgregarAsync(It.IsAny<Usuario>(), default), Times.Never);
    }

    [Fact]
    public async Task Registro_exitoso_devuelve_token_jwt_valido()
    {
        _repositorioMock
            .Setup(r => r.ExistePorEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        _repositorioMock
            .Setup(r => r.AgregarAsync(It.IsAny<Usuario>(), default))
            .Returns(Task.CompletedTask);

        var resultado = await CrearServicio().RegistrarAsync(new RegistroDto
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        Assert.False(string.IsNullOrEmpty(resultado.Token));
        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(resultado.Token));
        var jwt = handler.ReadJwtToken(resultado.Token);
        Assert.Equal("nostalgia-backend", jwt.Issuer);
    }

    [Fact]
    public async Task Login_con_email_inexistente_lanza_unauthorized()
    {
        _repositorioMock
            .Setup(r => r.ObtenerPorEmailAsync("noexiste@example.com", default))
            .ReturnsAsync((Usuario?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearServicio().LoginAsync(new LoginDto
            {
                Email = "noexiste@example.com",
                Contraseña = "contraseña123"
            }));
    }

    [Fact]
    public async Task Login_con_contraseña_incorrecta_lanza_unauthorized()
    {
        var hashCorrecto = BCrypt.Net.BCrypt.HashPassword("contraseña_correcta");
        _repositorioMock
            .Setup(r => r.ObtenerPorEmailAsync("test@example.com", default))
            .ReturnsAsync(new Usuario_Personal
            {
                Id = 1, Username = "testuser",
                Email = "test@example.com",
                Contraseña = hashCorrecto
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearServicio().LoginAsync(new LoginDto
            {
                Email = "test@example.com",
                Contraseña = "contraseña_incorrecta"
            }));
    }

    [Fact]
    public async Task Login_exitoso_devuelve_datos_del_usuario()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("contraseña123");
        _repositorioMock
            .Setup(r => r.ObtenerPorEmailAsync("test@example.com", default))
            .ReturnsAsync(new Usuario_Personal
            {
                Id = 5, Username = "testuser",
                Email = "test@example.com",
                Contraseña = hash
            });

        var resultado = await CrearServicio().LoginAsync(new LoginDto
        {
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        Assert.Equal(5, resultado.UsuarioId);
        Assert.Equal("testuser", resultado.Username);
        Assert.False(string.IsNullOrEmpty(resultado.Token));
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Tests de INTEGRACION — prueban el flujo HTTP completo (validaciones, codigos)
// ──────────────────────────────────────────────────────────────────────────────
public class AuthIntegracionTests
{
    [Fact]
    public async Task Registro_exitoso_devuelve_201()
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
        Assert.True(body.UsuarioId > 0);
        Assert.Equal("testuser", body.Username);
    }

    [Fact]
    public async Task Registro_con_email_duplicado_devuelve_409()
    {
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        var response = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "otrouser",
            Email = "test@example.com",
            Contraseña = "otracontraseña123"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("", "test@example.com", "contraseña123")]
    [InlineData("ab", "test@example.com", "contraseña123")]
    [InlineData("user", "no-es-email", "contraseña123")]
    [InlineData("user", "", "contraseña123")]
    [InlineData("user", "test@example.com", "corta")]
    [InlineData("user", "test@example.com", "")]
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
    }

    [Fact]
    public async Task Login_exitoso_devuelve_200()
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
            Contraseña = "contraseña123"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthRespuesta>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.Token));
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

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "contraseña123")]
    [InlineData("no-es-email", "contraseña123")]
    [InlineData("test@example.com", "")]
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

    private sealed class AuthFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
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
}
