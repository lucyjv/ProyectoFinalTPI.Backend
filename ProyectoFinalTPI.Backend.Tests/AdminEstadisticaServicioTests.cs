using Moq;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminEstadisticaServicioTests
{
    [Fact]
    public async Task QueObtenerValideLimiteYDelegue()
    {
        var repositorio = new Mock<IAdminEstadisticaRepositorio>();
        repositorio.Setup(r => r.Obtener(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstadisticasAdminDto { TotalPublicaciones = 3 });
        var resultado = await new AdminEstadisticaServicio(repositorio.Object).Obtener(10);
        Assert.Equal(3, resultado.TotalPublicaciones);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task QueRechaceLimitesInvalidos(int limite)
    {
        var servicio = new AdminEstadisticaServicio(Mock.Of<IAdminEstadisticaRepositorio>());
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.Obtener(limite));
    }
}
