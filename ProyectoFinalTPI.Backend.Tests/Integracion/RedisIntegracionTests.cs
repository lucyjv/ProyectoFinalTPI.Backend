using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Cache;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Entidades;
using Xunit;
namespace ProyectoFinalTPI.Backend.Tests.Integracion;
[Trait("Categoria", "Integracion")]
public class RedisIntegracionTests : IAsyncLifetime, IClassFixture<PostgisFixture>
{
    private readonly IContainer _redis = new ContainerBuilder().WithImage("redis:7.4-alpine").WithPortBinding(6379, true).WithCommand("redis-server", "--appendonly", "yes", "--appendfsync", "always").Build();
    private readonly PostgisFixture _postgres;
    private string Conexion => $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)},abortConnect=false,connectTimeout=1000,syncTimeout=1000,asyncTimeout=1000,connectRetry=1";
    public RedisIntegracionTests(PostgisFixture postgres) => _postgres = postgres;
    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        using var conexion = await ConnectionMultiplexer.ConnectAsync(Conexion);
        await conexion.GetDatabase().PingAsync();
    }
    public async Task DisposeAsync() => await _redis.DisposeAsync();
    private RedisCache Crear(string prefijo) => new(Options.Create(new RedisCacheOptions { Configuration = Conexion, InstanceName = prefijo }));

    [Fact]
    public async Task QueLaSesionSeRecupereDesdeOtroClienteConTtlYRespeteAlPropietario()
    {
        //Arrange
        var prefijo = Guid.NewGuid()+":";
        using var cliente = Crear(prefijo);
        using var otroCliente = Crear(prefijo);
        var repositorio = new SesionFypRedisRepositorio(cliente);
        //Act
        var creada = repositorio.Crear(7, new[] { 8, 3, 8 });
        var result = new SesionFypRedisRepositorio(otroCliente).Obtener(creada.Id, 7);
        using var conexion = await ConnectionMultiplexer.ConnectAsync(Conexion);
        var clave = prefijo+$"fyp:sesion:v1:{creada.Id:N}";
        var ttl = await conexion.GetDatabase().KeyTimeToLiveAsync(clave);
        //Assert
        Assert.NotNull(result);
        Assert.Equal(new[] { 8, 3 }, result.PublicacionesIds);
        Assert.Null(repositorio.Obtener(creada.Id, 9));
        Assert.InRange(ttl!.Value, TimeSpan.FromMinutes(29), TimeSpan.FromMinutes(30));
        // La expiración se provoca en Redis, sin esperar treinta minutos.
        await conexion.GetDatabase().KeyExpireAsync(clave, TimeSpan.Zero);
        Assert.Null(repositorio.Obtener(creada.Id, 7));
    }

    [Fact]
    public async Task QueGuardarUnCambioEnPostgresInvalideElPerfilCompartidoEnRedis()
    {
        //Arrange
        var prefijo = Guid.NewGuid()+":";
        using var cliente = Crear(prefijo);
        var cache = new CacheLecturasPublicas(cliente, NullLogger<CacheLecturasPublicas>.Instance);
        var conexion = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Database = "redis_test_"+Guid.NewGuid().ToString("N"), Pooling = false }.ConnectionString;
        var interceptor = new InvalidacionPublicaInterceptor(cache, new DetectorCambiosPublicos());
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(conexion, o=>o.UseNetTopologySuite()).AddInterceptors(interceptor).Options;
        await using var db = new ApplicationDbContext(opciones);
        await db.Database.MigrateAsync();
        var usuario = new Usuario { Username = "Antes", Email = "privado@example.com", Contraseña = "hash-privado" };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        var consulta = new PerfilPublicoConsulta(new OrigenLecturasPublicas(db), cache);
        var inicial = await consulta.Obtener(usuario.Id);
        //Act
        usuario.Username = "Despues";
        await db.SaveChangesAsync();
        using var otroCliente = Crear(prefijo);
        await using var otroContexto = new ApplicationDbContext(opciones);
        var otraConsulta = new PerfilPublicoConsulta(new OrigenLecturasPublicas(otroContexto), new CacheLecturasPublicas(otroCliente, NullLogger<CacheLecturasPublicas>.Instance));
        var result = await otraConsulta.Obtener(usuario.Id);
        //Assert
        Assert.Equal("Antes", inicial!.Username);
        Assert.Equal("Despues", result!.Username);
        Assert.True(string.IsNullOrEmpty(result.Contraseña));
        Assert.True(string.IsNullOrEmpty(result.Email));
        Assert.Null(await new OrigenLecturasPublicas(db).ObtenerUsuario(int.MaxValue, default));
        Assert.False(await new OrigenLecturasPublicas(db).EsPublicacionVisible(int.MaxValue, default));
    }

    [Fact]
    public async Task QueLaSesionPersistaAlReiniciarRedisConAof()
    {
        //Arrange
        var prefijo = Guid.NewGuid()+":";
        using var cliente = Crear(prefijo);
        var creada = new SesionFypRedisRepositorio(cliente).Crear(7, new[] { 8, 3 });
        //Act
        await _redis.StopAsync();
        await _redis.StartAsync();
        using var nuevoCliente = Crear(prefijo);
        var result = new SesionFypRedisRepositorio(nuevoCliente).Obtener(creada.Id, 7);
        //Assert
        Assert.NotNull(result);
        Assert.Equal(creada.Id, result.Id);
        Assert.Equal(creada.FechaExpiracion, result.FechaExpiracion);
        Assert.Equal(new[] { 8, 3 }, result.PublicacionesIds);
    }

    [Fact]
    public async Task QueDuranteUnaInterrupcionLasLecturasUsenPostgresYLasSesionesSeRecuperenAlVolverRedis()
    {
        //Arrange
        var prefijo = Guid.NewGuid()+":";
        using var cliente = Crear(prefijo);
        var sesiones = new SesionFypRedisRepositorio(cliente);
        var creada = sesiones.Crear(7, new[] { 4, 2 });
        var cache = new CacheLecturasPublicas(cliente, NullLogger<CacheLecturasPublicas>.Instance);
        var conexion = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Database = "redis_caida_"+Guid.NewGuid().ToString("N"), Pooling = false }.ConnectionString;
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(conexion, o => o.UseNetTopologySuite()).Options;
        await using var db = new ApplicationDbContext(opciones);
        await db.Database.MigrateAsync();
        var usuario = new Usuario { Username = "Disponible", Email = "prueba@example.com", Contraseña = "hash" };
        db.Usuarios.Add(usuario); await db.SaveChangesAsync();
        var perfil = new PerfilPublicoConsulta(new OrigenLecturasPublicas(db), cache);
        //Act
        await _redis.StopAsync();
        var result = await perfil.Obtener(usuario.Id);
        var errorLectura = Record.Exception(() => sesiones.Obtener(creada.Id, 7));
        var errorEscritura = Record.Exception(() => sesiones.Crear(7, new[] { 9 }));
        await _redis.StartAsync();
        using var recuperado = Crear(prefijo);
        var sesionRecuperada = new SesionFypRedisRepositorio(recuperado).Obtener(creada.Id, 7);
        var perfilRecuperado = await new PerfilPublicoConsulta(new OrigenLecturasPublicas(db), new CacheLecturasPublicas(recuperado, NullLogger<CacheLecturasPublicas>.Instance)).Obtener(usuario.Id);
        //Assert
        Assert.Equal("Disponible", result!.Username);
        Assert.IsType<AlmacenSesionNoDisponibleException>(errorLectura);
        Assert.IsType<AlmacenSesionNoDisponibleException>(errorEscritura);
        Assert.Equal(new[] { 4, 2 }, sesionRecuperada!.PublicacionesIds);
        Assert.Equal("Disponible", perfilRecuperado!.Username);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueUnaPublicacionOcultaOEliminadaNoDevuelvaElResumenQuePermaneceEnRedis(bool ocultar)
    {
        //Arrange
        var prefijo = Guid.NewGuid()+":";
        using var cliente = Crear(prefijo);
        var cache = new CacheLecturasPublicas(cliente, NullLogger<CacheLecturasPublicas>.Instance);
        var conexion = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Database = "redis_visibilidad_"+Guid.NewGuid().ToString("N"), Pooling = false }.ConnectionString;
        // Sin interceptor para probar que la protección de visibilidad funciona incluso con una copia vieja.
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(conexion, o => o.UseNetTopologySuite()).Options;
        await using var db = new ApplicationDbContext(opciones);
        await db.Database.MigrateAsync();
        var publicacion = new Publicacion {
            Titulo = "Recuerdo", Descripcion = "Historia", Fecha = DateTime.UtcNow,
            Usuario = new Usuario_Personal { Username = "Autor", Email = "autor@example.com", Contraseña = "hash", Nombre = "Autor", Apellido = "Prueba", FechaNacimiento = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)) },
            Lugar = new Lugar { Nombre = "Buenos Aires", Direccion = "Buenos Aires", Coordenadas = new NetTopologySuite.Geometries.Point(-58.4, -34.6) { SRID = 4326 } }
        };
        db.Publicaciones.Add(publicacion); await db.SaveChangesAsync();
        var consulta = new ResumenPublicacionConsulta(new OrigenLecturasPublicas(db), new PublicacionRepositorio(db), cache);
        var inicial = await consulta.Obtener(publicacion.Id);
        using var redis = await ConnectionMultiplexer.ConnectAsync(Conexion);
        var generacionBytes = await redis.GetDatabase().HashGetAsync(prefijo+"publico:generacion:v1", "data");
        var clave = prefijo+"publico:"+generacionBytes.ToString()+":resumen:"+publicacion.Id;
        //Act
        if (ocultar) publicacion.EstaOculto = true; else db.Publicaciones.Remove(publicacion);
        await db.SaveChangesAsync();
        await using var otraDb = new ApplicationDbContext(opciones);
        var result = await new ResumenPublicacionConsulta(new OrigenLecturasPublicas(otraDb), new PublicacionRepositorio(otraDb), cache).Obtener(publicacion.Id);
        //Assert
        Assert.NotNull(inicial);
        Assert.True(await redis.GetDatabase().KeyExistsAsync(clave));
        Assert.Null(result);
    }
}

