using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using StackExchange.Redis;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class SesionFypRedisRepositorioTests
{
    private readonly Mock<IDistributedCache> _cache = new(MockBehavior.Strict);
    private SesionFypRedisRepositorio Crear() => new(_cache.Object);

    [Fact]
    public async Task QueSeGuardeLaSesionSinDuplicadosConElOrdenOriginalYTreintaMinutosDeDuracion()
    {
        //Arrange
        byte[]? contenido = null;
        DistributedCacheEntryOptions? opciones = null;
        _cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, bytes, config, _) => { contenido = bytes; opciones = config; }).Returns(Task.CompletedTask);
        var repositorio = Crear();
        var antes = DateTime.UtcNow;
        //Act
        var result = await repositorio.CrearAsync(7, new[] { 3, 1, 3, 2 });
        //Assert
        Assert.Equal(new[] { 3, 1, 2 }, result.PublicacionesIds);
        Assert.Equal(7, result.UsuarioId);
        Assert.InRange(result.FechaExpiracion, antes.AddMinutes(30), DateTime.UtcNow.AddMinutes(30));
        Assert.Equal(result.FechaExpiracion, opciones!.AbsoluteExpiration!.Value.UtcDateTime);
        using var json = JsonDocument.Parse(contenido!);
        Assert.Equal(7, json.RootElement.GetProperty("UsuarioId").GetInt32());
        Assert.Equal(new[] { 3, 1, 2 }, json.RootElement.GetProperty("Publicaciones").EnumerateArray().Select(x => x.GetInt32()));
        _cache.Verify(c => c.SetAsync($"fyp:sesion:v1:{result.Id:N}", It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueSeRecupereLaSesionDelPropietarioSinAlterarSuOrden()
    {
        //Arrange
        var id = Guid.NewGuid();
        var expira = DateTime.UtcNow.AddMinutes(5);
        _cache.Setup(c => c.GetAsync($"fyp:sesion:v1:{id:N}", It.IsAny<CancellationToken>())).ReturnsAsync(JsonSerializer.SerializeToUtf8Bytes(new { Id = id, UsuarioId = 7, Expira = expira, Publicaciones = new[] { 9, 3 } }));
        var repositorio = Crear();
        //Act
        var result = await repositorio.ObtenerAsync(id, 7);
        //Assert
        Assert.NotNull(result);
        Assert.Equal(new[] { 9, 3 }, result.PublicacionesIds);
        Assert.Equal(expira, result.FechaExpiracion);
        _cache.Verify(c => c.GetAsync($"fyp:sesion:v1:{id:N}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("ausente")]
    [InlineData("corrupta")]
    [InlineData("nula")]
    [InlineData("otroUsuario")]
    [InlineData("otroId")]
    [InlineData("vencida")]
    [InlineData("sinPublicaciones")]
    public async Task QueNoSeDevuelvaUnaSesionInexistenteInvalidaOAjena(string caso)
    {
        //Arrange
        var id = Guid.NewGuid();
        byte[]? bytes = caso switch
        {
            "ausente" => null,
            "corrupta" => Encoding.UTF8.GetBytes("invalido"),
            "nula" => Encoding.UTF8.GetBytes("null"),
            _ => JsonSerializer.SerializeToUtf8Bytes(new { Id = caso == "otroId" ? Guid.NewGuid() : id, UsuarioId = caso == "otroUsuario" ? 8 : 7, Expira = DateTime.UtcNow.AddMinutes(caso == "vencida" ? -1 : 5), Publicaciones = caso == "sinPublicaciones" ? null : new[] { 1 } })
        };
        _cache.Setup(c => c.GetAsync($"fyp:sesion:v1:{id:N}", It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
        var repositorio = Crear();
        //Act
        var result = await repositorio.ObtenerAsync(id, 7);
        //Assert
        Assert.Null(result);
        _cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueSeInformeLaIndisponibilidadAlFallarLaLecturaOEscritura(bool escritura)
    {
        //Arrange
        var error = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "offline");
        if (escritura) _cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(error);
        else _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(error);
        var repositorio = Crear();
        //Act
        var result = await Record.ExceptionAsync(async () => { if (escritura) await repositorio.CrearAsync(1, new[] { 2 }); else await repositorio.ObtenerAsync(Guid.NewGuid(), 1); });
        //Assert
        Assert.Same(error, Assert.IsType<AlmacenSesionNoDisponibleException>(result).InnerException);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task QueSeRechacenIdentificadoresInvalidosSinAccederAlAlmacen(int usuarioId, int publicacionId)
    {
        //Arrange
        var repositorio = Crear();
        //Act
        var result = await Record.ExceptionAsync(() => repositorio.CrearAsync(usuarioId, new[] { publicacionId }));
        //Assert
        Assert.IsType<ArgumentException>(result);
        _cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueSeRechaceUnaColeccionNulaSinAccederAlAlmacen()
    {
        //Arrange
        var repositorio = Crear();
        //Act
        var result = await Record.ExceptionAsync(() => repositorio.CrearAsync(1, null!));
        //Assert
        Assert.IsType<ArgumentNullException>(result);
        _cache.VerifyNoOtherCalls();
    }
}
