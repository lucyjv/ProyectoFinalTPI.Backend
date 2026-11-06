using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminPublicacionRepositorioTests
{
    [Fact]
    public async Task QueListePaginadoYFiltreCategoriaIncluyendoOcultas()
    {
        await using var context = CrearContexto();
        var usuario = new Usuario_Personal { Username = "autor", Email = "autor@test.com" };
        context.AddRange(
            CrearPublicacion(usuario, "Cine visible", CategoriaEnum.Cine, false),
            CrearPublicacion(usuario, "Cine oculta", CategoriaEnum.Cine, true),
            CrearPublicacion(usuario, "Música", CategoriaEnum.Música, false));
        await context.SaveChangesAsync();
        var resultado = await new AdminPublicacionRepositorio(context).Listar(
            new FiltroPublicacionesAdminDto { Categoria = CategoriaEnum.Cine, Pagina = 1, Tamanio = 10 });
        Assert.Equal(2, resultado.TotalItems);
        Assert.Contains(resultado.Items, p => p.EstaOculto);
    }

    [Fact]
    public async Task QueEliminarQuiteLaPublicacionDePostgres()
    {
        await using var context = CrearContexto();
        var publicacion = CrearPublicacion(new Usuario_Personal { Username = "autor", Email = "autor@test.com" },
            "Recuerdo", CategoriaEnum.Lugares, false);
        context.Add(publicacion);
        await context.SaveChangesAsync();
        await new AdminPublicacionRepositorio(context).Eliminar(publicacion);
        Assert.False(await context.Publicaciones.AnyAsync());
    }

    private static Publicacion CrearPublicacion(Usuario usuario, string titulo, CategoriaEnum categoria, bool oculta) => new()
    {
        Titulo = titulo, Descripcion = "Descripción", Categoria = categoria, EstaOculto = oculta,
        Fecha = DateTime.UtcNow, FechaCreación = DateTime.UtcNow, Usuario = (Usuario_Interactivo)usuario,
        Lugar = new Lugar { Nombre = "Lugar", Coordenadas = new Point(0, 0) { SRID = 4326 } }
    };

    private static ApplicationDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
