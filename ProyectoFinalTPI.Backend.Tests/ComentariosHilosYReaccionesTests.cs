using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ComentariosHilosYReaccionesTests
{
    [Fact]
    public async Task ObtieneRespuestasYReaccionDelUsuario()
    {
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var raiz = new Comentario { Contenido = "RaÃ­z", PublicacionId = publicacion.Id, UsuarioId = autor.Id };
        context.Comentarios.Add(raiz);
        await context.SaveChangesAsync();
        context.Comentarios.Add(new Comentario
        {
            Contenido = "Respuesta",
            PublicacionId = publicacion.Id,
            UsuarioId = lector.Id,
            ComentarioPadreId = raiz.Id
        });
        context.ReaccionesComentario.Add(new ReaccionComentario
        {
            ComentarioId = raiz.Id,
            UsuarioId = lector.Id,
            Tipo = TipoReaccionComentario.Love
        });
        await context.SaveChangesAsync();

        var repositorio = new PublicacionRepositorio(context);
        var resultado = await repositorio.ObtenerHilosComentarios(publicacion.Id, null, 20, lector.Id);

        var comentario = Assert.Single(resultado);
        Assert.Equal("love", comentario.ReaccionActual);
        Assert.Equal(1, comentario.Reacciones["love"]);
        Assert.Equal("Respuesta", Assert.Single(comentario.Respuestas).Contenido);
    }

    [Fact]
    public async Task CambiarYQuitarReaccionMantieneUnaPorUsuario()
    {
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var comentario = new Comentario { Contenido = "Comentario", PublicacionId = publicacion.Id, UsuarioId = autor.Id };
        context.Comentarios.Add(comentario);
        await context.SaveChangesAsync();
        var repositorio = new PublicacionRepositorio(context);

        await repositorio.EstablecerReaccionComentario(comentario.Id, lector.Id, TipoReaccionComentario.Like);
        await repositorio.EstablecerReaccionComentario(comentario.Id, lector.Id, TipoReaccionComentario.Surprise);

        var reaccion = Assert.Single(await context.ReaccionesComentario.ToListAsync());
        Assert.Equal(TipoReaccionComentario.Surprise, reaccion.Tipo);
        await repositorio.QuitarReaccionComentario(comentario.Id, lector.Id);
        Assert.Empty(await context.ReaccionesComentario.ToListAsync());
    }

    private static ApplicationDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(Publicacion Publicacion, Usuario_Personal Autor, Usuario_Personal Lector)> CrearEscenario(ApplicationDbContext context)
    {
        var autor = new Usuario_Personal { Username = "autor", Email = "autor@test.com" };
        var lector = new Usuario_Personal { Username = "lector", Email = "lector@test.com" };
        var publicacion = new Publicacion
        {
            Titulo = "Recuerdo",
            Descripcion = "Historia",
            Fecha = DateTime.UtcNow,
            Usuario = autor,
            Lugar = new Lugar { Nombre = "Lugar", Coordenadas = new Point(0, 0) { SRID = 4326 } }
        };
        context.AddRange(autor, lector, publicacion);
        await context.SaveChangesAsync();
        return (publicacion, autor, lector);
    }
}
