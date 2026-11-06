using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class LogrosMigracionesIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    [Fact]
    public async Task RetiraSoloLosTresLogrosYConservaUsuariosYLosOtrosDesbloqueos()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = $"logros_migracion_{Guid.NewGuid():N}", Pooling = false
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection.ConnectionString, options => options.UseNetTopologySuite()).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261025190612_CompletarConfiguracionLogros");
            Assert.Equal(20, await db.Logro.CountAsync());
            // Esta prueba se detiene en un esquema histórico. El modelo EF actual ya
            // incluye columnas posteriores (como FotoPerfilUrl), por eso sembramos
            // los datos con SQL compatible con la versión que se está probando.
            await db.Database.ExecuteSqlRawAsync("""
                WITH nuevo_usuario AS (
                    INSERT INTO "Usuario"
                        ("Username", "Email", "Contraseña", "EsAdmin", "FechaCreacion",
                         "tipo_usuario", "Nombre", "Apellido", "FechaNacimiento",
                         "PuntosNostalgia", "Reputacion")
                    VALUES
                        ('migracion', 'migracion@example.test', '', false, now(),
                         'Usuario_Personal', '', '', DATE '1990-01-01', 0, 0)
                    RETURNING "Id"
                )
                INSERT INTO logros_usuarios ("UsuarioId", "LogroId", "FechaDesbloqueo")
                SELECT nuevo_usuario."Id", logros.id, now()
                FROM nuevo_usuario
                CROSS JOIN (VALUES (1), (4), (5), (17)) AS logros(id);
                """);

            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(17, await db.Logro.CountAsync());
            Assert.False(await db.Logro.AnyAsync(logro => new[] { 4, 5, 17 }.Contains(logro.Id)));
            Assert.Equal(1, await db.Usuarios.CountAsync());
            Assert.Equal(1, (await db.Logros_Usuarios.SingleAsync()).LogroId);
            await db.Database.MigrateAsync();
            Assert.Equal(17, await db.Logro.CountAsync());
            Assert.Single(await db.Logros_Usuarios.ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
