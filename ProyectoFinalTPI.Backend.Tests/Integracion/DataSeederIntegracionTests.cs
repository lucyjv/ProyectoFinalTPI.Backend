using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class DataSeederIntegracionTests : IClassFixture<PostgisFixture>
{
    private readonly PostgisFixture _postgres;

    public DataSeederIntegracionTests(PostgisFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task QueSeCarguenUsuariosYPublicacionesAunqueYaExistaUnUsuario()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var env = factory.Services.GetRequiredService<IWebHostEnvironment>();
        env.EnvironmentName = "Development";
        context.Usuarios.Add(new Usuario
        {
            Id = 1000, Username = "Existente", Email = "existente@example.com",
            Contraseña = "credencial-original"
        });
        await context.SaveChangesAsync();

        //Act
        DataSeeder.Seed(factory.Services, env);
        context.ChangeTracker.Clear();
        var publicaciones = await context.Publicaciones.Include(p => p.Usuario).ToListAsync();
        var miguel = await context.Usuario_Personales.SingleAsync(u => u.Username == "Miguel");

        //Assert
        Assert.Equal(38, await context.Usuarios.CountAsync());
        Assert.Equal(42, publicaciones.Count);
        Assert.Equal(42, await context.Lugares.CountAsync());
        Assert.All(publicaciones, p => Assert.IsType<Usuario_Personal>(p.Usuario));
        Assert.False(string.IsNullOrWhiteSpace(miguel.Apellido));
        Assert.True(BCrypt.Net.BCrypt.Verify("password123", miguel.Contraseña));
        Assert.Equal("credencial-original", (await context.Usuarios.SingleAsync(u => u.Id == 1000)).Contraseña);
    }

    [Fact]
    public async Task QueEjecutarElSeederDosVecesNoDupliqueNiSobrescribaDatos()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(_postgres);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var env = factory.Services.GetRequiredService<IWebHostEnvironment>();
        env.EnvironmentName = "Development";
        DataSeeder.Seed(factory.Services, env);
        var miguel = await context.Usuario_Personales.SingleAsync(u => u.Username == "Miguel");
        miguel.Apellido = "Apellido editado";
        miguel.Contraseña = BCrypt.Net.BCrypt.HashPassword("otra-clave");
        await context.SaveChangesAsync();
        var idsAntes = await context.Publicaciones.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();

        //Act
        DataSeeder.Seed(factory.Services, env);
        context.ChangeTracker.Clear();
        var idsDespues = await context.Publicaciones.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();
        var result = await context.Usuario_Personales.SingleAsync(u => u.Username == "Miguel");

        //Assert
        Assert.Equal(37, await context.Usuarios.CountAsync());
        Assert.Equal(42, await context.Lugares.CountAsync());
        Assert.Equal(idsAntes, idsDespues);
        Assert.Equal("Apellido editado", result.Apellido);
        Assert.True(BCrypt.Net.BCrypt.Verify("otra-clave", result.Contraseña));
    }
}
