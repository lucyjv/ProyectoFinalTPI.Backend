using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class RecuperacionContrasenaTests
{
    private readonly Mock<IUsuarioRepositorio> _usuarios = new();
    private readonly Mock<IRecuperacionContrasenaRepositorio> _recuperaciones = new();
    private readonly Mock<IColaRecuperacionContrasena> _cola = new();
    private readonly RecuperacionContrasenaOptions _opciones = new() { Habilitado = true };
    private static readonly DateTime Ahora = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    private AuthServicio CrearServicio() => new(_usuarios.Object, Mock.Of<ISesionUsuarioRepositorio>(),
        new ConfigurationBuilder().Build(), Mock.Of<IUsuarioServicio>(), _recuperaciones.Object, _cola.Object,
        Options.Create(_opciones), new RelojFijo());

    [Fact]
    public async Task QueLaSolicitudSeEncoleSinConsultarSiLaCuentaExiste()
    {
        //Arrange
        _cola.Setup(c => c.EncolarSolicitud("ana@example.com")).Returns(true);

        //Act
        await CrearServicio().SolicitarRecuperacionAsync(new() { Email = "ANA@EXAMPLE.COM" });

        //Assert
        _cola.Verify(c => c.EncolarSolicitud("ana@example.com"), Times.Once);
        _usuarios.Verify(u => u.ObtenerPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _recuperaciones.Verify(r => r.CrearSiCorrespondeAsync(It.IsAny<RecuperacionContrasena>(),
            It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueLaRecuperacionDeshabilitadaOConColaLlenaDevuelvaIndisponibilidad(bool deshabilitada)
    {
        //Arrange
        _opciones.Habilitado = !deshabilitada;
        _cola.Setup(c => c.EncolarSolicitud(It.IsAny<string>())).Returns(false);

        //Act & Assert
        await Assert.ThrowsAsync<RecuperacionContrasenaNoDisponibleException>(() =>
            CrearServicio().SolicitarRecuperacionAsync(new() { Email = "ana@example.com" }));
        _usuarios.Verify(u => u.ObtenerPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueElEnlaceContengaElTokenPeroLaBaseSoloRecibaSuHash()
    {
        //Arrange
        _usuarios.Setup(u => u.ObtenerPorEmailAsync("ana@example.com", default)).ReturnsAsync(CrearUsuario());
        RecuperacionContrasena? guardada = null;
        _recuperaciones.Setup(r => r.CrearSiCorrespondeAsync(It.IsAny<RecuperacionContrasena>(),
                TimeSpan.FromSeconds(120), default))
            .Callback<RecuperacionContrasena, TimeSpan, CancellationToken>((r, _, _) => guardada = r)
            .ReturnsAsync(true);

        //Act
        var correo = await CrearServicio().PrepararRecuperacionAsync("ana@example.com");

        //Assert
        Assert.NotNull(correo);
        Assert.NotNull(guardada);
        var enlace = correo.Texto.Split('\n')[0].Split(": ", 2)[1];
        var uri = new Uri(enlace);
        var token = Uri.UnescapeDataString(uri.Fragment["#token=".Length..]);
        Assert.Empty(uri.Query);
        Assert.Equal(76, token.Length);
        Assert.StartsWith(guardada.Id.ToString("N") + ".", token);
        Assert.Equal(Hash(token), guardada.TokenHash);
        Assert.DoesNotContain(token, guardada.TokenHash);
        Assert.Equal(Ahora.AddMinutes(30), guardada.FechaExpiracion);
        Assert.Equal("ana@example.com", correo.Destinatario);
    }

    [Theory]
    [InlineData("inexistente")]
    [InlineData("eliminada")]
    [InlineData("suspendida")]
    public async Task QueNoSePrepareUnCorreoParaCuentasNoHabilitadas(string estado)
    {
        //Arrange
        var usuario = estado == "inexistente" ? null : CrearUsuario();
        if (usuario is not null)
        {
            usuario.EstaEliminado = estado == "eliminada";
            usuario.SuspendidoHasta = estado == "suspendida" ? Ahora.AddDays(1) : null;
        }
        _usuarios.Setup(u => u.ObtenerPorEmailAsync("ana@example.com", default)).ReturnsAsync(usuario);

        //Act
        var correo = await CrearServicio().PrepararRecuperacionAsync("ana@example.com");

        //Assert
        Assert.Null(correo);
        _recuperaciones.Verify(r => r.CrearSiCorrespondeAsync(It.IsAny<RecuperacionContrasena>(),
            It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueUnaSolicitudDentroDelIntervaloNoGenereOtroCorreo()
    {
        //Arrange
        _usuarios.Setup(u => u.ObtenerPorEmailAsync("ana@example.com", default)).ReturnsAsync(CrearUsuario());
        _recuperaciones.Setup(r => r.CrearSiCorrespondeAsync(It.IsAny<RecuperacionContrasena>(),
            It.IsAny<TimeSpan>(), default)).ReturnsAsync(false);

        //Act
        var correo = await CrearServicio().PrepararRecuperacionAsync("ana@example.com");

        //Assert
        Assert.Null(correo);
    }

    [Fact]
    public async Task QueRestablecerEnvieElHashBCryptYEncoleElAvisoDespuesDeGuardar()
    {
        //Arrange
        var (registro, token) = CrearRecuperacion();
        _recuperaciones.Setup(r => r.ObtenerPorIdAsync(registro.Id, default)).ReturnsAsync(registro);
        var guardada = false;
        string? hashClave = null;
        _recuperaciones.Setup(r => r.RestablecerAsync(registro.Id, Hash(token), It.IsAny<string>(), default))
            .Callback<Guid, string, string, CancellationToken>((_, _, hash, _) => { hashClave = hash; guardada = true; })
            .ReturnsAsync(true);
        _cola.Setup(c => c.EncolarAviso(registro.EmailDestino, registro.Id))
            .Callback(() => Assert.True(guardada)).Returns(true);

        //Act
        await CrearServicio().RestablecerContrasenaAsync(Datos(token));

        //Assert
        Assert.NotNull(hashClave);
        Assert.True(BCrypt.Net.BCrypt.Verify("nueva-clave-123", hashClave));
        _cola.Verify(c => c.EncolarAviso(registro.EmailDestino, registro.Id), Times.Once);
    }

    [Theory]
    [InlineData("vencido")]
    [InlineData("usado")]
    [InlineData("revocado")]
    [InlineData("hash-incorrecto")]
    [InlineData("cuenta-eliminada")]
    public async Task QueUnEnlaceInvalidoNoCambieLaContrasena(string estado)
    {
        //Arrange
        var (registro, token) = CrearRecuperacion();
        if (estado == "vencido") registro.FechaExpiracion = Ahora;
        if (estado == "usado") registro.FechaUso = Ahora;
        if (estado == "revocado") registro.FechaRevocacion = Ahora;
        if (estado == "hash-incorrecto") registro.TokenHash = Hash("otro-token");
        if (estado == "cuenta-eliminada") registro.Usuario.EstaEliminado = true;
        _recuperaciones.Setup(r => r.ObtenerPorIdAsync(registro.Id, default)).ReturnsAsync(registro);

        //Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => CrearServicio().RestablecerContrasenaAsync(Datos(token)));
        _recuperaciones.Verify(r => r.RestablecerAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cola.Verify(c => c.EncolarAviso(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task QueSiOtroPedidoConsumioElEnlaceNoSeInformeUnCambioExitoso()
    {
        //Arrange
        var (registro, token) = CrearRecuperacion();
        _recuperaciones.Setup(r => r.ObtenerPorIdAsync(registro.Id, default)).ReturnsAsync(registro);
        _recuperaciones.Setup(r => r.RestablecerAsync(registro.Id, Hash(token), It.IsAny<string>(), default)).ReturnsAsync(false);

        //Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => CrearServicio().RestablecerContrasenaAsync(Datos(token)));
        _cola.Verify(c => c.EncolarAviso(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData("corta", "corta")]
    [InlineData("nueva-clave-123", "otra-clave-123")]
    public async Task QueLaClaveNuevaSeValideTambienEnElServicio(string clave, string confirmacion)
    {
        //Arrange
        var dto = Datos("token");
        dto.NuevaContrasena = clave;
        dto.ConfirmacionContrasena = confirmacion;

        //Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => CrearServicio().RestablecerContrasenaAsync(dto));
        _recuperaciones.Verify(r => r.ObtenerPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Usuario_Personal CrearUsuario() => new() { Id = 10, Username = "ana", Email = "ana@example.com" };
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static RestablecerContrasenaDto Datos(string token) => new()
    {
        Token = token, NuevaContrasena = "nueva-clave-123", ConfirmacionContrasena = "nueva-clave-123"
    };
    private static (RecuperacionContrasena Registro, string Token) CrearRecuperacion()
    {
        var registro = new RecuperacionContrasena
        {
            UsuarioId = 10, Usuario = CrearUsuario(), EmailDestino = "ana@example.com",
            FechaCreacion = Ahora, FechaExpiracion = Ahora.AddMinutes(30)
        };
        var token = registro.Id.ToString("N") + "." + new string('a', 43);
        registro.TokenHash = Hash(token);
        return (registro, token);
    }
    private sealed class RelojFijo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Ahora);
    }
}
