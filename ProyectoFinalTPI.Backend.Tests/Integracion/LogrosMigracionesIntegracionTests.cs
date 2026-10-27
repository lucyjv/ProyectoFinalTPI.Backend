using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ProyectoFinalTPI.Backend.Entidades;
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
            await db.GetService<IMigrator>().MigrateAsync("20261027164752_AgregarFotoPerfilUsuario");
            Assert.Equal(20, await db.Logro.CountAsync());
            var usuario = new Usuario_Personal
            {
                Username = "migracion", Email = "migracion@example.test", FechaNacimiento = new DateOnly(1990, 1, 1)
            };
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
            foreach (var id in new[] { 1, 4, 5, 17 }) db.Logros_Usuarios.Add(new Logros_Usuarios
            {
                UsuarioId = usuario.Id, LogroId = id, FechaDesbloqueo = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

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
