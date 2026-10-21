using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class GestionDuplicadosMomentoServicioTests
{
    private readonly Mock<IGestionDuplicadosMomentoRepositorio> _repositorio = new(MockBehavior.Strict);
    private readonly GestionDuplicadosMomentoServicio _servicio;
    public GestionDuplicadosMomentoServicioTests() => _servicio = new(_repositorio.Object, NullLogger<GestionDuplicadosMomentoServicio>.Instance);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task QueNoSeBusquenDuplicadosConParametrosInvalidos(int id, int limite)
    {
        //Arrange
        var ct = CancellationToken.None;

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.BuscarCandidatos(id, limite, ct));

        //Assert
        _repositorio.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 2, 1, 1)]
    [InlineData(1, 1, 1, 1)]
    [InlineData(1, 2, 0, 1)]
    [InlineData(1, 2, 1, 0)]
    public async Task QueNoSeFusionenIdsOVersionesInvalidas(int origen, int destino, long versionOrigen, long versionDestino)
    {
        //Arrange
        var solicitud = new SolicitudFusionMomentoDto(destino, versionOrigen, versionDestino);

        //Act
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Fusionar(origen, solicitud, 7, CancellationToken.None));

        //Assert
        _repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueSeEnvienLasVersionesYLaCancelacionSinModificarLaSeleccionAdministrativa()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();
        var solicitud = new SolicitudFusionMomentoDto(2, 3, 4);
        var esperado = new ResultadoFusionMomentoDto(1, 2, 5, 1, 5, false);
        _repositorio.Setup(r => r.Fusionar(1, solicitud, cancelacion.Token)).ReturnsAsync(esperado);

        //Act
        var result = await _servicio.Fusionar(1, solicitud, 7, cancelacion.Token);

        //Assert
        Assert.Same(esperado, result);
        _repositorio.VerifyAll();
    }

    [Fact]
    public async Task QueLaVistaPreviaSeDelegueSinEjecutarUnaFusion()
    {
        //Arrange
        var esperado = new VistaFusionMomentoDto(new(1, "México 86", null, null, 1), new(2, "Argentina campeón 1986", null, null, 1), 5, 0, 0, false);
        _repositorio.Setup(r => r.Previsualizar(1, 2, CancellationToken.None)).ReturnsAsync(esperado);

        //Act
        var result = await _servicio.Previsualizar(1, 2, CancellationToken.None);

        //Assert
        Assert.Same(esperado, result);
        _repositorio.VerifyAll();
    }
}
