using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminComentarioRepositorioTests
{
    [Fact]
    public async Task QueEliminarSeaLogicoVacieContenidoYQuiteReacciones()
    {
        await using var context = CrearContexto();
        var autor = new Usuario_Personal { Username = "autor", Email = "autor@test.com" };
        var publicacion = new Publicacion { Titulo = "P", Usuario = autor, Lugar = new Lugar { Nombre = "L" } };
        var comentario = new Comentario { Contenido = "Contenido", Usuario = autor, Publicacion = publicacion };
        comentario.Reacciones.Add(new ReaccionComentario { Usuario = autor, Tipo = TipoReaccionComentario.Like });
        context.Add(comentario);
        await context.SaveChangesAsync();
        await new AdminComentarioRepositorio(context).Eliminar(comentario);
        Assert.True(comentario.EstaEliminado);
        Assert.True(comentario.EstaOculto);
        Assert.Empty(comentario.Contenido);
        Assert.Empty(await context.ReaccionesComentario.ToListAsync());
    }

    private static ApplicationDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
