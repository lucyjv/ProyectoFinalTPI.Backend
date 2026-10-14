using Testcontainers.PostgreSql;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

// Un servidor temporal para la clase de tests. Cada factory crea su propia base.
public sealed class PostgisFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    public string ConnectionString => _contenedor.GetConnectionString();

    public Task InitializeAsync() => _contenedor.StartAsync();

    public async Task DisposeAsync()
    {
        await _contenedor.DisposeAsync();
    }
}
