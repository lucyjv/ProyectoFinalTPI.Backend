using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class PerfilEdicionTests
{
    private readonly Mock<IUsuarioRepositorio> _usuarios = new();
    private readonly Mock<IFotoPerfilAlmacenamiento> _fotos = new();
    private readonly Usuario _usuario = new() { Id = 7, Username = "original" };

    private UsuarioServicio CrearServicio()
    {
        _usuarios.Setup(r => r.ObtenerPorIdAsync(7)).ReturnsAsync(_usuario);
        _usuarios.Setup(r => r.ActualizarAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new UsuarioServicio(_usuarios.Object, Mock.Of<ILogroServicio>(),
            fotoPerfilAlmacenamiento: _fotos.Object);
    }

    [Fact]
    public async Task CambiarUsernameActualizaSoloAlUsuarioDelIdAutenticado()
    {
        var servicio = CrearServicio();
        var perfil = await servicio.CambiarUsernamePropioAsync(7, " nuevo ");

        Assert.Equal("nuevo", perfil.Username);
        Assert.Equal("nuevo", _usuario.Username);
        _usuarios.Verify(r => r.ActualizarAsync(
            It.Is<Usuario>(u => u.Id == 7 && u.Username == "nuevo"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CambiarUsernameRechazaUnoOcupadoSinGuardar()
    {
        var servicio = CrearServicio();
        _usuarios.Setup(r => r.ObtenerPorUsernameAsync("ocupado", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuario { Id = 8, Username = "ocupado" });

        await Assert.ThrowsAsync<NombreUsuarioEnUsoException>(
            () => servicio.CambiarUsernamePropioAsync(7, "ocupado"));
        Assert.Equal("original", _usuario.Username);
        _usuarios.Verify(r => r.ActualizarAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CambiarFotoGuardaUrlDevueltaPorAlmacenamiento()
    {
        var servicio = CrearServicio();
        _fotos.Setup(f => f.SubirAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://res.cloudinary.com/test/avatar.png");
        using var imagen = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2 });

        var perfil = await servicio.CambiarFotoPerfilPropiaAsync(7, imagen, "image/png");

        Assert.Equal("https://res.cloudinary.com/test/avatar.png", perfil.FotoPerfilUrl);
        Assert.Equal(perfil.FotoPerfilUrl, _usuario.FotoPerfilUrl);
        _fotos.Verify(f => f.SubirAsync(It.IsAny<Stream>(),
            It.Is<string>(n => n.StartsWith("usuario-7-") && n.EndsWith(".png")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CambiarFotoRechazaContenidoDisfrazadoSinSubirlo()
    {
        var servicio = CrearServicio();
        using var archivo = new MemoryStream("not an image"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.CambiarFotoPerfilPropiaAsync(7, archivo, "image/png"));
        _fotos.VerifyNoOtherCalls();
    }
}
