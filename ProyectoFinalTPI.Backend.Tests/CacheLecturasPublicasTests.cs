using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using ProyectoFinalTPI.Backend.Repositorio.Cache;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CacheLecturasPublicasTests
{
    private const string Generacion = "publico:generacion:v1";
    private readonly Mock<IDistributedCache> _almacen = new(MockBehavior.Strict);
    private readonly Mock<ILogger<CacheLecturasPublicas>> _logger = new();
    private readonly Mock<IFuente> _fuente = new(MockBehavior.Strict);
    private CacheLecturasPublicas Crear() => new(_almacen.Object, _logger.Object);
    public interface IFuente { Task<string?> Consultar(); }
    private void PrepararGeneracion() => _almacen.Setup(a => a.GetAsync(Generacion, It.IsAny<CancellationToken>())).ReturnsAsync(Encoding.UTF8.GetBytes("actual"));
    private static RedisConnectionException Caida() => new(ConnectionFailureType.UnableToConnect, "Sin conexión");

    [Fact]
    public async Task QueSeDevuelvaLaEntradaCacheadaSinConsultarLaFuente()
    {
        //Arrange
        PrepararGeneracion();
        _almacen.Setup(a => a.GetAsync("publico:actual:perfil", It.IsAny<CancellationToken>())).ReturnsAsync(JsonSerializer.SerializeToUtf8Bytes("guardado"));
        var servicio = Crear();
        //Act
        var result = await servicio.Obtener("perfil", _fuente.Object.Consultar, default);
        //Assert
        Assert.Equal("guardado", result);
        _fuente.Verify(f => f.Consultar(), Times.Never);
        _almacen.Verify(a => a.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("json-invalido")]
    public async Task QueSeReconstruyaUnaEntradaAusenteOCorruptaConVencimientoDeDosMinutos(string? contenido)
    {
        //Arrange
        PrepararGeneracion();
        _almacen.Setup(a => a.GetAsync("publico:actual:perfil", It.IsAny<CancellationToken>())).ReturnsAsync(contenido is null ? null : Encoding.UTF8.GetBytes(contenido));
        _fuente.Setup(f => f.Consultar()).ReturnsAsync("nuevo");
        _almacen.Setup(a => a.SetAsync("publico:actual:perfil", It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var servicio = Crear();
        //Act
        var result = await servicio.Obtener("perfil", _fuente.Object.Consultar, default);
        //Assert
        Assert.Equal("nuevo", result);
        _fuente.Verify(f => f.Consultar(), Times.Once);
        _almacen.Verify(a => a.SetAsync("publico:actual:perfil", It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "\"nuevo\""), It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(2)), default), Times.Once);
    }

    [Fact]
    public async Task QueNoSeCacheeUnResultadoInexistente()
    {
        //Arrange
        PrepararGeneracion();
        _almacen.Setup(a => a.GetAsync("publico:actual:perfil", default)).ReturnsAsync((byte[]?)null);
        _fuente.Setup(f => f.Consultar()).ReturnsAsync((string?)null);
        var servicio = Crear();
        //Act
        var result = await servicio.Obtener("perfil", _fuente.Object.Consultar, default);
        //Assert
        Assert.Null(result);
        _almacen.Verify(a => a.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueSeConsulteLaFuenteCuandoRedisNoEsteDisponible()
    {
        //Arrange
        _almacen.Setup(a => a.GetAsync(Generacion, default)).ThrowsAsync(Caida());
        _fuente.Setup(f => f.Consultar()).ReturnsAsync("base");
        var servicio = Crear();
        //Act
        var result = await servicio.Obtener("perfil", _fuente.Object.Consultar, default);
        //Assert
        Assert.Equal("base", result);
        _fuente.Verify(f => f.Consultar(), Times.Once);
    }

    [Fact]
    public async Task QueSeDevuelvaElDatoAunqueFalleSuAlmacenamientoEnRedis()
    {
        //Arrange
        PrepararGeneracion();
        _almacen.Setup(a => a.GetAsync("publico:actual:perfil", default)).ReturnsAsync((byte[]?)null);
        _fuente.Setup(f => f.Consultar()).ReturnsAsync("base");
        _almacen.Setup(a => a.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default)).ThrowsAsync(Caida());
        var servicio = Crear();
        //Act
        var result = await servicio.Obtener("perfil", _fuente.Object.Consultar, default);
        //Assert
        Assert.Equal("base", result);
        _fuente.Verify(f => f.Consultar(), Times.Once);
    }

    [Fact]
    public async Task QueUnaLecturaEnCursoNoEscribaEnLaGeneracionNueva()
    {
        //Arrange
        PrepararGeneracion();
        _almacen.Setup(a => a.GetAsync("publico:actual:perfil", default)).ReturnsAsync((byte[]?)null);
        var pendiente = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _fuente.Setup(f => f.Consultar()).Returns(pendiente.Task);
        _almacen.Setup(a => a.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default)).Returns(Task.CompletedTask);
        var servicio = Crear();
        //Act
        var lectura = servicio.Obtener("perfil", _fuente.Object.Consultar, default);
        await servicio.InvalidarAsync(default);
        pendiente.SetResult("anterior");
        await lectura;
        //Assert
        _almacen.Verify(a => a.SetAsync(Generacion, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Once);
        _almacen.Verify(a => a.SetAsync("publico:actual:perfil", It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Once);
        _almacen.Verify(a => a.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task QueSePropagueLaCancelacionSinConsultarLaFuente()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        _almacen.Setup(a => a.GetAsync(Generacion, cancelacion.Token)).ThrowsAsync(new OperationCanceledException(cancelacion.Token));
        var servicio = Crear();
        //Act
        var result = await Record.ExceptionAsync(() => servicio.Obtener("perfil", _fuente.Object.Consultar, cancelacion.Token));
        //Assert
        Assert.IsAssignableFrom<OperationCanceledException>(result);
        _fuente.Verify(f => f.Consultar(), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueSeIntenteCrearLaGeneracionYSeUseLaFuenteSiNoSePuedeRecuperar(bool disponible)
    {
        //Arrange
        _almacen.SetupSequence(a => a.GetAsync(Generacion, default))
            .ReturnsAsync((byte[]?)null)
            .ReturnsAsync(disponible ? Encoding.UTF8.GetBytes("nueva") : null);
        _almacen.Setup(a => a.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default)).Returns(Task.CompletedTask);
        if (disponible) _almacen.Setup(a => a.GetAsync("publico:nueva:perfil", default)).ReturnsAsync((byte[]?)null);
        _fuente.Setup(f => f.Consultar()).ReturnsAsync("base");
        var servicio = Crear();
        //Act
        var result = await servicio.Obtener("perfil", _fuente.Object.Consultar, default);
        //Assert
        Assert.Equal("base", result);
        _fuente.Verify(f => f.Consultar(), Times.Once);
        _almacen.Verify(a => a.SetAsync(Generacion, It.Is<byte[]>(b => b.Length == 32), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Once);
        _almacen.Verify(a => a.SetAsync("publico:nueva:perfil", It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), disponible ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task QueLaInvalidacionCambieLaGeneracionOTolereUnaCaidaDeRedis(bool asincrona, bool falla)
    {
        //Arrange
        if (asincrona)
        {
            var configuracion = _almacen.Setup(a => a.SetAsync(Generacion, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default));
            if (falla) configuracion.ThrowsAsync(Caida()); else configuracion.Returns(Task.CompletedTask);
        }
        else
        {
            var configuracion = _almacen.Setup(a => a.Set(Generacion, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>()));
            if (falla) configuracion.Throws(Caida());
        }
        var servicio = Crear();
        //Act
        var result = await Record.ExceptionAsync(async () => { if (asincrona) await servicio.InvalidarAsync(default); else servicio.Invalidar(); });
        //Assert
        Assert.Null(result);
        if (asincrona) _almacen.Verify(a => a.SetAsync(Generacion, It.Is<byte[]>(b => b.Length == 32), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Once);
        else _almacen.Verify(a => a.Set(Generacion, It.Is<byte[]>(b => b.Length == 32), It.IsAny<DistributedCacheEntryOptions>()), Times.Once);
    }

    [Fact]
    public async Task QueNoSeOculteUnErrorDeLaFuenteNiSeCacheeComoResultado()
    {
        //Arrange
        PrepararGeneracion();
        _almacen.Setup(a => a.GetAsync("publico:actual:perfil", default)).ReturnsAsync((byte[]?)null);
        var error = new InvalidOperationException("Error de consulta");
        _fuente.Setup(f => f.Consultar()).ThrowsAsync(error);
        var servicio = Crear();
        //Act
        var result = await Record.ExceptionAsync(() => servicio.Obtener("perfil", _fuente.Object.Consultar, default));
        //Assert
        Assert.Same(error, result);
        _almacen.Verify(a => a.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Never);
    }
}
