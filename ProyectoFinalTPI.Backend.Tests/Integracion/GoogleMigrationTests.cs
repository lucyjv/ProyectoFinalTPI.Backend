using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Servicio;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

public class GoogleMigrationTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    [Fact]
    public async Task SoloLasCuentasPreviasQuedanHabilitadasYLaVinculacionEsAtomica()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgres.ConnectionString, o => o.UseNetTopologySuite()).Options;
        await using var db = new ApplicationDbContext(options);
        var migrations = db.Database.GetMigrations().ToArray();
        var googleIndex = Array.FindIndex(migrations, m => m.EndsWith("AgregarAccesoGoogle"));
        Assert.True(googleIndex > 0);
        await db.GetService<IMigrator>().MigrateAsync(migrations[googleIndex - 1]);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Usuario" ("Username", "Email", "Contraseña", "EsAdmin", "EstaEliminado", "FechaCreacion", tipo_usuario)
            VALUES ('maria', 'maria@gmail.com', 'existing-hash', FALSE, FALSE, NOW(), 'Usuario');
            """);
        await db.Database.MigrateAsync();
        var maria = await db.Usuarios.SingleAsync();
        Assert.True(maria.PuedeVincularGoogle);
        Assert.False(maria.SoloGoogle);
        var newLocal = new Usuario { Username = "nueva", Email = "nueva@gmail.com", Contraseña = "new-hash" };
        db.Usuarios.Add(newLocal);
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.False((await db.Usuarios.SingleAsync(u => u.Id == newLocal.Id)).PuedeVincularGoogle);
        var repository = new UsuarioRepositorio(db);
        Assert.True(await repository.VincularGoogleAsync(maria.Id, "google-maria"));
        Assert.False(await repository.VincularGoogleAsync(maria.Id, "another-google"));
        Assert.False(await repository.VincularGoogleAsync(newLocal.Id, "google-nueva"));
        db.ChangeTracker.Clear();
        Assert.Equal(maria.Id, (await repository.ObtenerPorGoogleAsync("google-maria"))!.Id);

        // The daily-login service saves tracked users during session creation. The newly
        // linked subject must survive that save (ExecuteUpdate alone leaves it stale).
        var legacy = new Usuario_Personal { Username = "legacy", Email = "legacy@gmail.com", Contraseña = "existing-hash", PuedeVincularGoogle = true };
        db.Usuarios.Add(legacy);
        await db.SaveChangesAsync();
        var profile = new Mock<IUsuarioServicio>();
        profile.Setup(p => p.RegistrarIngresoDiarioAsync(legacy.Id, default)).Returns(async () => {
            db.Usuarios.Update(legacy);
            await db.SaveChangesAsync();
            return new List<LogroEstadoDto>();
        });
        var auth = new AuthServicio(repository, new SesionUsuarioRepositorio(db),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Jwt:Clave"] = "testing-google-key-of-at-least-32-characters" }).Build(),
            profile.Object, Mock.Of<IRecuperacionContrasenaRepositorio>(), Mock.Of<IColaRecuperacionContrasena>(),
            Options.Create(new RecuperacionContrasenaOptions()), TimeProvider.System);
        var session = await auth.GoogleAsync(new IdentidadGoogle("legacy-sub", legacy.Email, true), null);
        Assert.Equal(legacy.Id, session!.Respuesta.UsuarioId);
        db.ChangeTracker.Clear();
        Assert.Equal("legacy-sub", (await db.Usuarios.SingleAsync(u => u.Id == legacy.Id)).GoogleSubject);

    }
}
