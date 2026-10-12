using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

public sealed class PublicacionesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private bool _baseInicializada;

    public PublicacionesApiFactory(PostgisFixture postgres)
    {
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

