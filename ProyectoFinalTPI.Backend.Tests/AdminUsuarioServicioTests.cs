using Moq;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminUsuarioServicioTests
{
    private readonly Mock<IAdminUsuarioRepositorio> _repositorio = new();
    private readonly Mock<ISesionUsuarioRepositorio> _sesiones = new();

    [Fact]
    public async Task QueDarDeBajaSeaLogicoYRevoqueSesiones()
    {
        var usuario = new Usuario_Personal { Id = 5, Username = "usuario" };
        _repositorio.Setup(r => r.Obtener(5, It.IsAny<CancellationToken>())).ReturnsAsync(usuario);
        await new AdminUsuarioServicio(_repositorio.Object, _sesiones.Object).DarDeBaja(5, 1);
        Assert.True(usuario.EstaEliminado);
        Assert.NotNull(usuario.FechaEliminacion);
        _sesiones.Verify(s => s.RevocarTodasAsync(5, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNoPermitaBajaPropia()
    {
        var servicio = new AdminUsuarioServicio(_repositorio.Object, _sesiones.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.DarDeBaja(1, 1));
        _repositorio.Verify(r => r.Obtener(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueSuspenderEstablezcaFechaYRevoqueSesiones()
    {
        var usuario = new Usuario_Personal { Id = 5 };
        _repositorio.Setup(r => r.Obtener(5, It.IsAny<CancellationToken>())).ReturnsAsync(usuario);
        await new AdminUsuarioServicio(_repositorio.Object, _sesiones.Object)
            .CambiarSuspension(5, 1, new CambiarSuspensionUsuarioDto { Suspender = true, Dias = 10 });
        Assert.True(usuario.SuspendidoHasta > DateTime.UtcNow.AddDays(9));
        _sesiones.Verify(s => s.RevocarTodasAsync(5, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
