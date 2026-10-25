using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Neo4j.Driver;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

public sealed class Neo4jLogrosFixture : IAsyncLifetime
{
    private readonly IContainer _container = new ContainerBuilder()
        .WithImage("neo4j:5-community")
        .WithEnvironment("NEO4J_AUTH", "none")
        .WithEnvironment("NEO4J_server_memory_heap_initial__size", "256m")
        .WithEnvironment("NEO4J_server_memory_heap_max__size", "512m")
        .WithEnvironment("NEO4J_server_memory_pagecache_size", "256m")
        .WithPortBinding(7687, true).WithPortBinding(7474, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(7474)))
        .Build();
    public IDriver Driver { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Driver = GraphDatabase.Driver($"bolt://{_container.Hostname}:{_container.GetMappedPublicPort(7687)}", AuthTokens.None);
        await Driver.VerifyConnectivityAsync();
    }
    public async Task DisposeAsync()
    {
        if (Driver != null) await Driver.DisposeAsync();
        await _container.DisposeAsync();
    }
}
