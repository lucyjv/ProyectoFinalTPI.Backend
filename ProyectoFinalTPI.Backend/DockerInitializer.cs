using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend;

public static class DockerInitializer
{
    public static async Task EjecutarAsync(IServiceProvider services, IWebHostEnvironment environment)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        if (!File.Exists(Path.Combine(environment.ContentRootPath, "seed.json")))
            throw new InvalidOperationException("Falta seed.json para inicializar el entorno Docker.");
        DataSeeder.Seed(services, environment);

        var driver = scope.ServiceProvider.GetRequiredService<IDriver>();
        await driver.VerifyConnectivityAsync();
        await using var session = driver.AsyncSession();
        // Restricciones idempotentes; los nodos usan las PK reales, nunca los IDs del JSON.
        await (await session.RunAsync("CREATE CONSTRAINT usuario_postgres_id IF NOT EXISTS FOR (u:Usuario) REQUIRE u.postgresId IS UNIQUE")).ConsumeAsync();
        await (await session.RunAsync("CREATE CONSTRAINT publicacion_postgres_id IF NOT EXISTS FOR (p:Publicacion) REQUIRE p.postgresId IS UNIQUE")).ConsumeAsync();
        var usuarios = await db.Usuarios.AsNoTracking().Select(u => new { u.Id }).ToListAsync();
        var publicaciones = await db.Publicaciones.AsNoTracking().Select(p => new { p.Id }).ToListAsync();
        await session.ExecuteWriteAsync(async tx =>
        {
            await (await tx.RunAsync("UNWIND $ids AS id MERGE (:Usuario {postgresId: id})",
                new { ids = usuarios.Select(u => u.Id).ToArray() })).ConsumeAsync();
            await (await tx.RunAsync("UNWIND $ids AS id MERGE (:Publicacion {postgresId: id})",
                new { ids = publicaciones.Select(p => p.Id).ToArray() })).ConsumeAsync();
        });
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DockerInitializer")
            .LogInformation("Docker inicializado: {Usuarios} usuarios y {Publicaciones} publicaciones sincronizadas con Neo4j.", usuarios.Count, publicaciones.Count);
    }
}
