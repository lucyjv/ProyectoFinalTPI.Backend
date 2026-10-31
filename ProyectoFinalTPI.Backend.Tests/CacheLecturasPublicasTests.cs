using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using ProyectoFinalTPI.Backend.Repositorio.Cache;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CacheLecturasPublicasTests
{
    private static CacheLecturasPublicas Crear() => new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<CacheLecturasPublicas>.Instance);
    [Fact]
    public async Task ReutilizaLaLecturaPeroNoComparteObjetosMutables()
    {
        var cache = Crear(); var consultas = 0;
        Task<List<int>?> Fuente() { consultas++; return Task.FromResult<List<int>?>(new() { 1 }); }
        (await cache.Obtener("perfil", Fuente, default))!.Add(2);
        Assert.Equal(new[] { 1 }, await cache.Obtener("perfil", Fuente, default));
        Assert.Equal(1, consultas);
        await cache.InvalidarAsync(default);
        await cache.Obtener("perfil", Fuente, default);
        Assert.Equal(2, consultas);
    }

    [Fact]
    public async Task UnaLecturaAnteriorALaInvalidacionNoRepueblaLaNuevaGeneracion()
    {
        var cache = Crear();
        var inicio = new TaskCompletionSource(); var liberar = new TaskCompletionSource<string?>();
        var vieja = cache.Obtener("resumen", async () => { inicio.SetResult(); return await liberar.Task; }, default);
        await inicio.Task;
        await cache.InvalidarAsync(default);
        liberar.SetResult("viejo"); await vieja;
        Assert.Equal("nuevo", await cache.Obtener("resumen", () => Task.FromResult<string?>("nuevo"), default));
    }

    [Fact]
    public async Task RedisCaidoPermiteLeerLaFuente()
    {
        var almacen = new Mock<IDistributedCache>();
        almacen.Setup(a => a.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "offline"));
        var cache = new CacheLecturasPublicas(almacen.Object, NullLogger<CacheLecturasPublicas>.Instance);
        Assert.Equal("base", await cache.Obtener("resumen", () => Task.FromResult<string?>("base"), default));
    }
}
