using Testcontainers.PostgreSql;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

// Un servidor temporal para la clase de tests. Cada factory crea su propia base.
public sealed class PostgisFixture : IAsyncLifetime
{
    public string ConnectionString => "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=60lD1r06e71";

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;
}
