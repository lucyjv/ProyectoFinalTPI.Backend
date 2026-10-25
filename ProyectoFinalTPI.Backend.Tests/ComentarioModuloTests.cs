using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ComentarioModuloTests
{
    [Fact]
    public async Task PermiteResponderUnaRespuestaSinLimiteDeProfundidad()
    {
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var servicio = CrearServicio(context);

        var raiz = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Raíz", UsuarioId = autor.Id });
        var respuesta = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Respuesta", UsuarioId = lector.Id, ComentarioPadreId = raiz.Id });
        var nieta = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Tercer nivel", UsuarioId = autor.Id, ComentarioPadreId = respuesta.Id });

        Assert.Equal(respuesta.Id, nieta.ComentarioPadreId);
        Assert.Equal(3, await context.Comentarios.CountAsync());
    }

    [Fact]
    public async Task EntregaSoloTresRespuestasInicialesYUnCursorParaContinuar()
    {
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var raiz = new Comentario
        {
            Contenido = "Raíz", PublicacionId = publicacion.Id, UsuarioId = autor.Id,
            FechaCreacion = DateTime.UtcNow.AddHours(-1)
        };
        context.Comentarios.Add(raiz);
        await context.SaveChangesAsync();
        for (var indice = 0; indice < 5; indice++)
        {
            context.Comentarios.Add(new Comentario
            {
                Contenido = $"Respuesta {indice}", PublicacionId = publicacion.Id, UsuarioId = lector.Id,
                ComentarioPadreId = raiz.Id, FechaCreacion = DateTime.UtcNow.AddMinutes(indice)
            });
        }
        await context.SaveChangesAsync();

        var pagina = await CrearServicio(context).ObtenerRaices(publicacion.Id, null, 20, lector.Id);
        var comentario = Assert.Single(pagina.Items);

        Assert.Equal(5, comentario.CantidadRespuestas);
        Assert.Equal(3, comentario.Respuestas.Count);
        Assert.NotNull(comentario.SiguienteCursorRespuestas);
    }

    [Fact]
    public async Task EliminarConservaElNodoSiTieneRespuestasYOcultaSuIdentidad()
    {
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var servicio = CrearServicio(context);
        var raiz = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Se elimina", UsuarioId = autor.Id });
        await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Se conserva", UsuarioId = lector.Id, ComentarioPadreId = raiz.Id });

        await servicio.Eliminar(publicacion.Id, raiz.Id, autor.Id);
        var eliminado = Assert.Single((await servicio.ObtenerRaices(publicacion.Id, null, 20, autor.Id)).Items);

        Assert.True(eliminado.EstaEliminado);
        Assert.Null(eliminado.Contenido);
        Assert.Null(eliminado.Autor);
        Assert.Single(eliminado.Respuestas);
    }

    [Fact]
    public async Task ImpideEliminarUnComentarioAjeno()
    {
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var servicio = CrearServicio(context);
        var comentario = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Propio", UsuarioId = autor.Id });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            servicio.Eliminar(publicacion.Id, comentario.Id, lector.Id));
    }

    [Fact]
    public async Task CambiarYQuitarReaccionMantieneUnaPorUsuario()
    {
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var servicio = CrearServicio(context);
        var comentario = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Comentario", UsuarioId = autor.Id });

        await servicio.EstablecerReaccion(publicacion.Id, comentario.Id, lector.Id, "like");
        var actualizado = await servicio.EstablecerReaccion(publicacion.Id, comentario.Id, lector.Id, "surprise");
        Assert.Equal("surprise", actualizado.ReaccionActual);
        Assert.Equal(1, actualizado.Reacciones["surprise"]);

        var sinReaccion = await servicio.QuitarReaccion(publicacion.Id, comentario.Id, lector.Id);
        Assert.Null(sinReaccion.ReaccionActual);
        Assert.Empty(sinReaccion.Reacciones);
    }

    private static ComentarioServicio CrearServicio(ApplicationDbContext context) =>
        new(new ComentarioRepositorio(context), new UsuarioRepositorio(context), Mock.Of<INotificacionServicio>());

    private static ApplicationDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(Publicacion Publicacion, Usuario_Personal Autor, Usuario_Personal Lector)>
        CrearEscenario(ApplicationDbContext context)
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
