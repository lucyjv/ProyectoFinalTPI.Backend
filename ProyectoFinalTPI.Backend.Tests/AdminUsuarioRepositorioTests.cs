using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminUsuarioRepositorioTests
{
    [Fact]
    public async Task QueBusquePorNombreYExcluyaBajasPorDefecto()
    {
        await using var context = CrearContexto();
        context.Usuarios.AddRange(
            new Usuario_Personal { Username = "juan", Email = "juan@test.com", Nombre = "Francisco" },
            new Usuario_Personal { Username = "baja", Email = "baja@test.com", Nombre = "Francisco", EstaEliminado = true });
        await context.SaveChangesAsync();
        var resultado = await new AdminUsuarioRepositorio(context).Listar(
            new FiltroUsuariosAdminDto { Texto = "francisco" });
        var usuario = Assert.Single(resultado.Items);
        Assert.Equal("juan", usuario.Username);
    }

    [Fact]
    public async Task QuePagineUsuariosEInformeTotal()
    {
        await using var context = CrearContexto();
        for (var i = 1; i <= 3; i++)
            context.Usuarios.Add(new Usuario_Personal { Username = $"u{i}", Email = $"u{i}@test.com" });
        await context.SaveChangesAsync();
        var resultado = await new AdminUsuarioRepositorio(context).Listar(
            new FiltroUsuariosAdminDto { Pagina = 2, Tamanio = 2 });
        Assert.Equal(3, resultado.TotalItems);
        Assert.Single(resultado.Items);
    }

    private static ApplicationDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
