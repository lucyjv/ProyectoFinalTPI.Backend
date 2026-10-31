using Microsoft.Extensions.Caching.Distributed;
using Xunit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Repositorio;

namespace ProyectoFinalTPI.Backend.Tests;

public class SesionFypRedisRepositorioTests
{
    [Fact]
    public void CaidaDelAlmacenSeDistingueDeSesionExpirada()
    {
        var cache = new Moq.Mock<IDistributedCache>();
        cache.Setup(c => c.Get(Moq.It.IsAny<string>())).Throws(
            new StackExchange.Redis.RedisConnectionException(StackExchange.Redis.ConnectionFailureType.UnableToConnect, "offline"));
        var repositorio = new SesionFypRedisRepositorio(cache.Object);
        Assert.Throws<ProyectoFinalTPI.Backend.Entidades.AlmacenSesionNoDisponibleException>(
            () => repositorio.Obtener(Guid.NewGuid(), 1));
    }

    [Fact]
    public void ConservaOrdenYPropietarioEntreInstanciasDelRepositorio()
    {
        IDistributedCache cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var primero = new SesionFypRedisRepositorio(cache);
        var segundo = new SesionFypRedisRepositorio(cache);
        var creada = primero.Crear(7, new[] { 3, 1, 3, 2 });
        var recuperada = segundo.Obtener(creada.Id, 7);
        Assert.NotNull(recuperada);
        Assert.Equal(new[] { 3, 1, 2 }, recuperada.PublicacionesIds);
        Assert.Equal(creada.FechaExpiracion, recuperada.FechaExpiracion);
        Assert.Null(segundo.Obtener(creada.Id, 8));
        Assert.InRange(creada.FechaExpiracion - DateTime.UtcNow, TimeSpan.FromMinutes(29), TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void UnaSesionEliminadaNoSeReconstruyeConOtroOrden()
    {
        IDistributedCache cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var repositorio = new SesionFypRedisRepositorio(cache);
        var creada = repositorio.Crear(1, new[] { 9, 4 });
        cache.Remove($"fyp:sesion:v1:{creada.Id:N}");
        Assert.Null(repositorio.Obtener(creada.Id, 1));
    }
}
