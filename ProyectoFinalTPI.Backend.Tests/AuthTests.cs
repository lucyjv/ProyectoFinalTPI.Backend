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
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AuthServicioTests
{
    private readonly Mock<IUsuarioRepositorio> _repositorioMock = new();
    private readonly Mock<ISesionUsuarioRepositorio> _sesionesMock = new();
    private readonly Mock<IUsuarioServicio> _usuarioServicioMock = new();
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

        _sesionesMock
            .Setup(r => r.AgregarAsync(It.IsAny<SesionUsuario>(), default))
            .Returns(Task.CompletedTask);
    }

    #region Registro

    [Fact]
    public async Task QueSeHasheeLaContraseniaAntesDePersistir()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExistePorEmailAsync("test@example.com", default))
            .ReturnsAsync(false);
        _repositorioMock
            .Setup(r => r.ExistePorUsernameAsync("testuser", default))
            .ReturnsAsync(false);

        Usuario? usuarioGuardado = null;
        _repositorioMock
            .Setup(r => r.AgregarAsync(It.IsAny<Usuario>(), default))
            .Callback<Usuario, CancellationToken>((u, _) => usuarioGuardado = u)
            .Returns(Task.CompletedTask);

        var dto = CrearRegistroDto("testuser", "test@example.com", "contraseña123");

        //Act
        await CrearServicio().RegistrarAsync(dto);

        //Assert
        Assert.NotNull(usuarioGuardado);
        Assert.NotEqual("contraseña123", usuarioGuardado.Contraseña);
        Assert.Matches(@"^\$2[ab]\$", usuarioGuardado.Contraseña);
    }

    [Fact]
    public async Task QueFalleElRegistroConEmailDuplicado()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExistePorEmailAsync("duplicado@example.com", default))
            .ReturnsAsync(true);

        var dto = CrearRegistroDto("testuser", "duplicado@example.com", "contraseña123");

        //Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio().RegistrarAsync(dto));

        _repositorioMock.Verify(r => r.AgregarAsync(It.IsAny<Usuario>(), default), Times.Never);
    }

    [Fact]
    public async Task QueFalleElRegistroConUsernameDuplicado()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExistePorEmailAsync("nuevo@example.com", default))
            .ReturnsAsync(false);
        _repositorioMock
            .Setup(r => r.ExistePorUsernameAsync("duplicado", default))
            .ReturnsAsync(true);

        var dto = CrearRegistroDto("duplicado", "nuevo@example.com", "contraseña123");

        //Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio().RegistrarAsync(dto));

        _repositorioMock.Verify(r => r.AgregarAsync(It.IsAny<Usuario>(), default), Times.Never);
    }

    [Fact]
    public async Task QueNoSeConsulteUsernameSiEmailYaEstaDuplicado()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExistePorEmailAsync("duplicado@example.com", default))
            .ReturnsAsync(true);

        var dto = CrearRegistroDto("cualquiera", "duplicado@example.com", "contraseña123");

        //Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio().RegistrarAsync(dto));

        _repositorioMock.Verify(
            r => r.ExistePorUsernameAsync(It.IsAny<string>(), default),
            Times.Never);
    }

    [Fact]
    public async Task QueElMensajeDeErrorIdentifiqueElCampoCuandoUsernameEstaDuplicado()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExistePorEmailAsync("nuevo@example.com", default))
            .ReturnsAsync(false);
        _repositorioMock
            .Setup(r => r.ExistePorUsernameAsync("duplicado", default))
            .ReturnsAsync(true);

        var dto = CrearRegistroDto("duplicado", "nuevo@example.com", "contraseña123");

        //Act
        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio().RegistrarAsync(dto));

        //Assert
        Assert.Contains("nombre de usuario", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueElRegistroExitosoDevuelvaTokenJwtValido()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ExistePorEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        _repositorioMock
            .Setup(r => r.ExistePorUsernameAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        _repositorioMock
            .Setup(r => r.AgregarAsync(It.IsAny<Usuario>(), default))
            .Returns(Task.CompletedTask);

        var dto = CrearRegistroDto("testuser", "test@example.com", "contraseña123");

        //Act
        var resultado = await CrearServicio().RegistrarAsync(dto);

        //Assert
        Assert.False(string.IsNullOrEmpty(resultado.Respuesta.Token));
        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(resultado.Respuesta.Token));
        var jwt = handler.ReadJwtToken(resultado.Respuesta.Token);
        Assert.Equal("nostalgia-backend", jwt.Issuer);
        Assert.NotNull(jwt.Claims.SingleOrDefault(claim => claim.Type == "sid"));
        Assert.False(string.IsNullOrWhiteSpace(resultado.RefreshToken));
    }

    #endregion

    #region Login

    [Fact]
    public async Task QueFalleElLoginConEmailInexistente()
    {
        //Arrange
        _repositorioMock
            .Setup(r => r.ObtenerPorEmailAsync("noexiste@example.com", default))
            .ReturnsAsync((Usuario?)null);

        var dto = CrearLoginDto("noexiste@example.com", "contraseña123");

        //Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearServicio().LoginAsync(dto));
    }

    [Fact]
    public async Task QueFalleElLoginConContraseniaIncorrecta()
    {
        //Arrange
        var hashCorrecto = BCrypt.Net.BCrypt.HashPassword("contraseña_correcta");
        _repositorioMock
            .Setup(r => r.ObtenerPorEmailAsync("test@example.com", default))
            .ReturnsAsync(new Usuario_Personal
            {
                Id = 1,
                Username = "testuser",
                Email = "test@example.com",
                Contraseña = hashCorrecto
            });

        var dto = CrearLoginDto("test@example.com", "contraseña_incorrecta");

        //Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearServicio().LoginAsync(dto));
    }

    [Fact]
    public async Task QueElLoginExitosoDevuelvaDatosDelUsuario()
    {
        //Arrange
        var hash = BCrypt.Net.BCrypt.HashPassword("contraseña123");
        _repositorioMock
            .Setup(r => r.ObtenerPorEmailAsync("test@example.com", default))
            .ReturnsAsync(new Usuario_Personal
            {
                Id = 5,
                Username = "testuser",
                Email = "test@example.com",
                Contraseña = hash
            });

        var dto = CrearLoginDto("test@example.com", "contraseña123");

        //Act
        var resultado = await CrearServicio().LoginAsync(dto);

        //Assert
        Assert.Equal(5, resultado.Respuesta.UsuarioId);
        Assert.Equal("testuser", resultado.Respuesta.Username);
        Assert.False(string.IsNullOrEmpty(resultado.Respuesta.Token));
    }

    #endregion

    #region Sesiones y Refresh

    [Fact]
    public async Task QueElRefreshRoteElTokenEInvalideElAnterior()
    {
        //Arrange
        var usuario = new Usuario_Personal
        {
            Id = 5,
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = BCrypt.Net.BCrypt.HashPassword("contraseña123")
        };
        _repositorioMock
            .Setup(r => r.ObtenerPorEmailAsync("test@example.com", default))
            .ReturnsAsync(usuario);

        SesionUsuario? sesion = null;
        _sesionesMock
            .Setup(r => r.AgregarAsync(It.IsAny<SesionUsuario>(), default))
            .Callback<SesionUsuario, CancellationToken>((valor, _) => sesion = valor)
            .Returns(Task.CompletedTask);

        var login = await CrearServicio().LoginAsync(CrearLoginDto("test@example.com", "contraseña123"));
        Assert.NotNull(sesion);

        _sesionesMock
            .Setup(r => r.ObtenerPorIdAsync(sesion.Id, default))
            .ReturnsAsync(sesion);
        _sesionesMock
            .Setup(r => r.RotarAsync(sesion, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .Callback<SesionUsuario, string, DateTime, DateTime, CancellationToken>((valor, hash, expiracion, ultimoUso, _) =>
            {
                valor.RefreshTokenHash = hash;
                valor.FechaExpiracion = expiracion;
                valor.UltimoUso = ultimoUso;
            })
            .ReturnsAsync(true);

        //Act
        var renovada = await CrearServicio().RenovarAsync(login.RefreshToken);

        //Assert
        Assert.NotEqual(login.RefreshToken, renovada.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearServicio().RenovarAsync(login.RefreshToken));
    }

    [Fact]
    public async Task QueElLogoutRevoqueLaSesionPersistida()
    {
        //Arrange
        var usuario = new Usuario_Personal
        {
            Id = 5,
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = BCrypt.Net.BCrypt.HashPassword("contraseña123")
        };
        _repositorioMock
            .Setup(r => r.ObtenerPorEmailAsync("test@example.com", default))
            .ReturnsAsync(usuario);

        SesionUsuario? sesion = null;
        _sesionesMock
            .Setup(r => r.AgregarAsync(It.IsAny<SesionUsuario>(), default))
            .Callback<SesionUsuario, CancellationToken>((valor, _) => sesion = valor)
            .Returns(Task.CompletedTask);

        var login = await CrearServicio().LoginAsync(CrearLoginDto("test@example.com", "contraseña123"));
        Assert.NotNull(sesion);
        _sesionesMock
            .Setup(r => r.ObtenerPorIdAsync(sesion.Id, default))
            .ReturnsAsync(sesion);

        //Act
        await CrearServicio().CerrarSesionAsync(login.RefreshToken);

        //Assert
        _sesionesMock.Verify(
            r => r.RevocarAsync(sesion, It.IsAny<DateTime>(), default),
            Times.Once);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private AuthServicio CrearServicio() =>
        new(_repositorioMock.Object, _sesionesMock.Object, _configuracion, _usuarioServicioMock.Object);

    private static RegistroDto CrearRegistroDto(string username, string email, string password) =>
        new() { Username = username, Email = email, Contraseña = password };

    private static LoginDto CrearLoginDto(string email, string password) =>
        new() { Email = email, Contraseña = password };

    #endregion
}

public class AuthIntegracionTests
{
    #region Registro HTTP

    [Fact]
    public async Task QueElRegistroExitosoDevuelva201()
    {
        //Arrange
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        //Act
        var response = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        //Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthRespuesta>();
        Assert.NotNull(body);
        Assert.True(body.UsuarioId > 0);
        Assert.Equal("testuser", body.Username);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueElRegistroConEmailDuplicadoDevuelva409()
    {
        //Arrange
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        //Act
        var response = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "otrouser",
            Email = "test@example.com",
            Contraseña = "otracontraseña123"
        });

        //Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task QueElRegistroConUsernameDuplicadoDevuelva409()
    {
        //Arrange
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "primero@example.com",
            Contraseña = "contraseña123"
        });

        //Act
        var response = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "segundo@example.com",
            Contraseña = "contraseña456"
        });

        //Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task QueElRegistroConUsernameDuplicadoEnDistintoCaseDevuelva409()
    {
        //Arrange
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "TestUser",
            Email = "primero@example.com",
            Contraseña = "contraseña123"
        });

        //Act
        var response = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "segundo@example.com",
            Contraseña = "contraseña456"
        });

        //Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("", "test@example.com", "contraseña123")]
    [InlineData("ab", "test@example.com", "contraseña123")]
    [InlineData("user", "no-es-email", "contraseña123")]
    [InlineData("user", "", "contraseña123")]
    [InlineData("user", "test@example.com", "corta")]
    [InlineData("user", "test@example.com", "")]
    public async Task QueElRegistroConDatosInvalidosDevuelva400(
        string username, string email, string contraseña)
    {
        //Arrange
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        //Act
        var response = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = username,
            Email = email,
            Contraseña = contraseña
        });

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Login y Sesiones HTTP

    [Fact]
    public async Task QueElLoginExitosoDevuelva200()
    {
        //Arrange
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        //Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthRespuesta>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.Token));
    }

    [Fact]
    public async Task QueElRefreshRoteCookieYLogoutImpidaVolverAUsarla()
    {
        //Arrange
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var registro = await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });
        var primeraCookie = Assert.Single(registro.Headers.GetValues("Set-Cookie")).Split(';')[0];

        //Act & Assert - Refresh
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", primeraCookie);
        var refresh = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var segundaCookie = Assert.Single(refresh.Headers.GetValues("Set-Cookie")).Split(';')[0];
        Assert.NotEqual(primeraCookie, segundaCookie);

        //Act & Assert - Logout
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", segundaCookie);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logoutRequest)).StatusCode);

        //Act & Assert - Refresh revocado
        using var refreshRevocadoRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRevocadoRequest.Headers.Add("Cookie", segundaCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(refreshRevocadoRequest)).StatusCode);
    }

    [Fact]
    public async Task QueElLoginConContraseniaIncorrectaDevuelva401()
    {
        //Arrange
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/registro", new
        {
            Username = "testuser",
            Email = "test@example.com",
            Contraseña = "contraseña123"
        });

        //Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "test@example.com",
            Contraseña = "contraseña_incorrecta"
        });

        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QueElLoginConEmailInexistenteDevuelva401()
    {
        //Arrange
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        //Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "noexiste@example.com",
            Contraseña = "contraseña123"
        });

        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "contraseña123")]
    [InlineData("no-es-email", "contraseña123")]
    [InlineData("test@example.com", "")]
    public async Task QueElLoginConDatosInvalidosDevuelva400(string email, string contraseña)
    {
        //Arrange
        await using var factory = new AuthFactory();
        using var client = factory.CreateClient();

        //Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Contraseña = contraseña
        });

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Clases Auxiliares

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

    #endregion
}
