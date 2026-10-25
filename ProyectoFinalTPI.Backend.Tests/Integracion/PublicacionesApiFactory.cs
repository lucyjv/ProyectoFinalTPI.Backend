using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Neo4j.Driver;
using Npgsql;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Tests;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

public sealed class PublicacionesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private bool _baseInicializada;
    private readonly IDriver? _driver;
    private readonly TimeProvider? _timeProvider;

    public PublicacionesApiFactory(PostgisFixture postgres, IDriver? driver = null, TimeProvider? timeProvider = null)
    {
        _driver = driver;
        _timeProvider = timeProvider;
        // La conexión proviene exclusivamente del contenedor, nunca de appsettings.
        _connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = $"publicaciones_test_{Guid.NewGuid():N}",
            Pooling = false
        }.ConnectionString;
    }

    public MediaFake Media { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o =>
                o.UseNpgsql(_connectionString, npgsql => npgsql.UseNetTopologySuite()));
            services.RemoveAll<IMultimediaServicio>();
            services.AddSingleton<IMultimediaServicio>(Media);
            services.RemoveAll<IDriver>();
            services.AddSingleton(_driver ?? CrearNeo4jDriverFake());
            if (_timeProvider != null) services.AddSingleton(_timeProvider);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        try
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _baseInicializada = true;
            db.Database.Migrate();
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            if (_baseInicializada)
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(_connectionString, npgsql => npgsql.UseNetTopologySuite())
                    .Options;
                await using var db = new ApplicationDbContext(options);
                await db.Database.EnsureDeletedAsync();
            }
        }
    }
    public async Task PrepararUsuario()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Usuarios.Add(new Usuario_Personal { Id = 1, Username = "test", Email = "test@example.com" });
        await db.SaveChangesAsync();
    }

    private static IDriver CrearNeo4jDriverFake()
    {
        var cursor = TestDispatchProxy<IResultCursor>.Create((metodo, _) =>
        {
            if (metodo.Name == nameof(IAsyncEnumerable<IRecord>.GetAsyncEnumerator))
                return new EmptyRecordEnumerator();
            if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                return ValueTask.CompletedTask;

            throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}.");
        });

        var queryRunner = TestDispatchProxy<IAsyncQueryRunner>.Create((metodo, _) =>
            metodo.Name == nameof(IAsyncQueryRunner.RunAsync)
                ? Task.FromResult(cursor)
                : throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}."));

        var session = TestDispatchProxy<IAsyncSession>.Create((metodo, argumentos) =>
        {
            if (metodo.Name == nameof(IAsyncSession.ExecuteReadAsync))
                return ((Delegate)argumentos![0]!).DynamicInvoke(queryRunner);
            if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                return ValueTask.CompletedTask;

            throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}.");
        });

        return TestDispatchProxy<IDriver>.Create((metodo, _) =>
        {
            if (metodo.Name == nameof(IDriver.AsyncSession))
                return session;
            if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                return ValueTask.CompletedTask;

            throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}.");
        });
    }

    private sealed class EmptyRecordEnumerator : IAsyncEnumerator<IRecord>
    {
        public IRecord Current => null!;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(false);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class MediaFake : IMultimediaServicio
{
    public int Calls;
    public bool Falla;
    public MultimediaEnum Tipo;
    public string? Nombre, Url;
    public byte[]? Bytes;
    public async Task<string> SubirAsync(Stream? archivo, string? nombreArchivo, string? urlExterna, MultimediaEnum tipo)
    {
        Calls++;
        if (Falla) throw new InvalidOperationException("Error simulado de multimedia");
        Tipo = tipo; Nombre = nombreArchivo; Url = urlExterna;
        if (archivo != null)
        {
            using var copia = new MemoryStream();
            await archivo.CopyToAsync(copia);
            Bytes = copia.ToArray();
        }
        return "https://media.example/test";
    }
}

