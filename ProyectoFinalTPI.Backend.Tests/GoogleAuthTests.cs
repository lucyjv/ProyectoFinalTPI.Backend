using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class GoogleAuthTests
{
    private readonly Mock<IUsuarioRepositorio> users = new();
    private readonly Mock<ISesionUsuarioRepositorio> sessions = new();
    private readonly Mock<IUsuarioServicio> profile = new();
    private readonly Mock<IRecuperacionContrasenaRepositorio> recovery = new();
    private static readonly IdentidadGoogle Maria = new("google-maria", "maria@gmail.com", true);
    private AuthServicio Service() => new(users.Object, sessions.Object,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Jwt:Clave"] = "testing-google-auth-key-of-at-least-32-characters" }).Build(), profile.Object,
        recovery.Object, Mock.Of<IColaRecuperacionContrasena>(),
        Options.Create(new RecuperacionContrasenaOptions { Habilitado = true }), TimeProvider.System);

    [Fact]
    public async Task MariaConservaIdYContraseñaAlVincularCuentaAnterior()
    {
        var maria = new Usuario_Personal { Id = 7, Email = Maria.Email, Username = "maria", PuedeVincularGoogle = true,
            Contraseña = BCrypt.Net.BCrypt.HashPassword("password123") };
        users.Setup(r => r.ObtenerPorEmailAsync(Maria.Email, default)).ReturnsAsync(maria);
        users.Setup(r => r.VincularGoogleAsync(7, Maria.Subject, default)).ReturnsAsync(true);
        var result = await Service().GoogleAsync(Maria, null);
        Assert.Equal(7, result!.Respuesta.UsuarioId);
        Assert.Equal("maria", result.Respuesta.Username);
        Assert.Equal(Maria.Subject, maria.GoogleSubject);
        Assert.True(BCrypt.Net.BCrypt.Verify("password123", maria.Contraseña));
        users.Verify(r => r.AgregarAsync(It.IsAny<Usuario>(), default), Times.Never);
        sessions.Verify(r => r.AgregarAsync(It.Is<SesionUsuario>(s => s.UsuarioId == 7), default), Times.Once);
        var local = await Service().LoginAsync(new LoginDto { Email = Maria.Email, Contraseña = "password123" });
        Assert.Equal(7, local.Respuesta.UsuarioId);
    }

    [Fact]
    public async Task PedroConOtroCorreoNecesitaUsernameAntesDeCrearCuentaIndependiente()
    {
        var google = new IdentidadGoogle("google-pedro", "pedro@gmail.com", true);
        Assert.Null(await Service().GoogleAsync(google, null));
        users.Verify(r => r.AgregarAsync(It.IsAny<Usuario>(), default), Times.Never);
        Usuario? created = null;
        users.Setup(r => r.AgregarAsync(It.IsAny<Usuario>(), default)).Callback<Usuario, CancellationToken>((u, _) => { u.Id = 22; created = u; }).Returns(Task.CompletedTask);
        var result = await Service().GoogleAsync(google, "pedro_nuevo");
        Assert.Equal(22, result!.Respuesta.UsuarioId);
        Assert.True(created!.SoloGoogle);
        Assert.False(created.PuedeVincularGoogle);
        Assert.Equal(google.Subject, created.GoogleSubject);
        Assert.Equal("", created.Contraseña);
    }

    [Fact]
    public async Task VictorNoPuedeIngresarNiRegistrarseConContraseña()
    {
        var victor = new Usuario_Personal { Id = 9, Email = "victor@gmail.com", Username = "victor", SoloGoogle = true, GoogleSubject = "google-victor" };
        users.Setup(r => r.ObtenerPorEmailAsync(victor.Email, default)).ReturnsAsync(victor);
        var login = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service().LoginAsync(new LoginDto { Email = victor.Email, Contraseña = "" }));
        Assert.Contains("Google", login.Message);
        var registration = await Assert.ThrowsAsync<InvalidOperationException>(() => Service().RegistrarAsync(new RegistroDto { Email = victor.Email, Username = "otra", Contraseña = "password123" }));
        Assert.Contains("Google", registration.Message);
        Assert.Null(await Service().PrepararRecuperacionAsync(victor.Email));
        recovery.Verify(r => r.CrearSiCorrespondeAsync(It.IsAny<RecuperacionContrasena>(), It.IsAny<TimeSpan>(), default), Times.Never);
    }

    [Theory]
    [InlineData(false, true, null)]
    [InlineData(true, false, null)]
    [InlineData(true, true, "another-subject")]
    public async Task NoVinculaCuentasNuevasCorreosExternosNiIdentidadesYaVinculadas(bool legacy, bool authoritative, string? subject)
    {
        users.Setup(r => r.ObtenerPorEmailAsync(Maria.Email, default)).ReturnsAsync(new Usuario_Personal {
            Id = 7, Email = Maria.Email, PuedeVincularGoogle = legacy, GoogleSubject = subject });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().GoogleAsync(Maria with { CorreoAcreditado = authoritative }, null));
        users.Verify(r => r.VincularGoogleAsync(It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task IdentificaPorSubjectAunqueGoogleCambieElEmail()
    {
        users.Setup(r => r.ObtenerPorGoogleAsync(Maria.Subject, default)).ReturnsAsync(new Usuario_Personal { Id = 7, Email = "anterior@gmail.com", Username = "maria" });
        Assert.Equal(7, (await Service().GoogleAsync(Maria, null))!.Respuesta.UsuarioId);
        users.Verify(r => r.ObtenerPorEmailAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RechazaCuentaEliminadaOSuspendida(bool deleted)
    {
        users.Setup(r => r.ObtenerPorGoogleAsync(Maria.Subject, default)).ReturnsAsync(new Usuario_Personal {
            Id = 7, EstaEliminado = deleted, SuspendidoHasta = deleted ? null : DateTime.UtcNow.AddDays(1) });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service().GoogleAsync(Maria, null));
        sessions.Verify(r => r.AgregarAsync(It.IsAny<SesionUsuario>(), default), Times.Never);
    }

    [Fact]
    public async Task UsernameOcupadoNoCreaUsuarioNiSesion()
    {
        users.Setup(r => r.ExistePorUsernameAsync("ocupado", default)).ReturnsAsync(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().GoogleAsync(Maria, "ocupado"));
        users.Verify(r => r.AgregarAsync(It.IsAny<Usuario>(), default), Times.Never);
        sessions.Verify(r => r.AgregarAsync(It.IsAny<SesionUsuario>(), default), Times.Never);
    }

    [Fact]
    public async Task RegistroLocalNuevoNoHabilitaVinculacion()
    {
        await Service().RegistrarAsync(new RegistroDto { Email = "nueva@gmail.com", Username = "nueva", Contraseña = "password123" });
        users.Verify(r => r.AgregarAsync(It.Is<Usuario>(u => !u.PuedeVincularGoogle && !u.SoloGoogle && u.GoogleSubject == null), default), Times.Once);
    }
}

public class GoogleIdentityVerifierTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("expired")]
    [InlineData("nonce")]
    [InlineData("unverified")]
    [InlineData("signature")]
    [InlineData("unsigned")]
    [InlineData("subject")]
    [InlineData("external")]
    public async Task ValidaFirmaAudienceIssuerExpiracionNonceYCorreo(string scenario)
    {
        using var rsa = RSA.Create(2048);
        using var otherRsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var metadata = new OpenIdConnectConfiguration();
        metadata.SigningKeys.Add(key);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Google:ClientId"] = "test-client" }).Build();
        var verifier = new GoogleIdentityVerifier(config, new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata));
        var claims = new[] { new Claim("sub", scenario == "subject" ? "" : "google-subject"), new Claim("email", scenario == "external" ? "user@other.test" : "user@gmail.com"),
            new Claim("email_verified", scenario == "unverified" ? "false" : "true", ClaimValueTypes.Boolean),
            new Claim("nonce", scenario == "nonce" ? "wrong" : "challenge") };
        var token = new JwtSecurityToken(scenario == "issuer" ? "https://attacker.test" : "https://accounts.google.com",
            scenario == "audience" ? "other-client" : "test-client", claims,
            DateTime.UtcNow.AddHours(-2), scenario == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(5),
            scenario == "unsigned" ? null : new SigningCredentials(scenario == "signature" ? new RsaSecurityKey(otherRsa) { KeyId = "test-key" } : key, SecurityAlgorithms.RsaSha256));
        var serialized = new JwtSecurityTokenHandler().WriteToken(token);
        if (scenario is "valid" or "external")
        {
            var result = await verifier.VerificarAsync(serialized, "challenge", default);
            Assert.Equal("google-subject", result.Subject);
            Assert.Equal(scenario == "valid", result.CorreoAcreditado);
        }
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => verifier.VerificarAsync(serialized, "challenge", default));
    }
}

public class GoogleControllerTests
{
    private static ProyectoFinalTPI.Backend.Controllers.AuthController Controller(Mock<IAuthServicio> auth)
    {
        var environment = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Testing");
        var controller = new ProyectoFinalTPI.Backend.Controllers.AuthController(auth.Object, environment.Object, Mock.Of<IUsuarioRepositorio>());
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public void SinClientIdNoPublicaGoogleNiCreaCookie()
    {
        var controller = Controller(new Mock<IAuthServicio>());
        var result = controller.GoogleConfig(new ConfigurationBuilder().Build(), new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        var json = System.Text.Json.JsonSerializer.Serialize(((Microsoft.AspNetCore.Mvc.OkObjectResult)result).Value);
        Assert.Contains("\"habilitado\":false", json);
        Assert.False(controller.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequiereDesafioProtegidoYConservaNonceHastaCompletarRegistro(bool signup)
    {
        var auth = new Mock<IAuthServicio>();
        var controller = Controller(auth);
        var protection = new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Google:ClientId"] = "client" }).Build();
        var result = (Microsoft.AspNetCore.Mvc.OkObjectResult)controller.GoogleConfig(config, protection);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        var nonce = payload.GetProperty("nonce").GetString()!;
        var cookie = controller.Response.Headers.SetCookie.ToString().Split(';')[0];
        Assert.Contains("httponly", controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        var verifier = new Mock<IGoogleIdentityVerifier>();
        verifier.Setup(v => v.VerificarAsync("credential", nonce, default)).ReturnsAsync(new IdentidadGoogle("sub", "user@gmail.com", true));
        auth.Setup(a => a.GoogleAsync(It.IsAny<IdentidadGoogle>(), null, It.IsAny<string?>(), default))
            .ReturnsAsync(signup ? null : new ResultadoAutenticacionDto { RefreshToken = "refresh", RefreshTokenExpira = DateTime.UtcNow.AddDays(30) });
        controller.Response.Headers.Clear();
        var missing = await controller.Google(new GoogleLoginDto { Credential = "credential" }, verifier.Object, protection, default);
        Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>(missing);
        verifier.Verify(v => v.VerificarAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        controller.Request.Headers.Cookie = cookie;
        var completed = await controller.Google(new GoogleLoginDto { Credential = "credential" }, verifier.Object, protection, default);
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(completed);
        if (signup)
        {
            Assert.Contains("requiereUsername", System.Text.Json.JsonSerializer.Serialize(((Microsoft.AspNetCore.Mvc.OkObjectResult)completed).Value));
            Assert.False(controller.Response.Headers.ContainsKey("Set-Cookie"));
        }
        else Assert.Contains("__Host-Nostalgiar.Refresh", controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task DesafioManipuladoNoLlegaAlVerificador()
    {
        var controller = Controller(new Mock<IAuthServicio>());
        controller.Request.Headers.Cookie = "__Host-Nostalgiar.Google=manipulado";
        var verifier = new Mock<IGoogleIdentityVerifier>();
        Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>(await controller.Google(new GoogleLoginDto { Credential = "x" }, verifier.Object,
            new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), default));
        verifier.Verify(v => v.VerificarAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }
}
