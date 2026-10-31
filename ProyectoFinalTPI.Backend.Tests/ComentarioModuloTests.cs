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
    #region Creación de Comentarios y Logros

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueComentariosConservenNotificacionesYLogros(bool esAutor)
    {
        //Arrange
        await using var context = CrearContexto();
        await context.Database.EnsureCreatedAsync();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var usuario = esAutor ? autor : lector;
        var notificaciones = new Mock<INotificacionServicio>();
        var servicio = new ComentarioServicio(
            new ComentarioRepositorio(context), new UsuarioRepositorio(context),
            notificaciones.Object, new LogroServicio(new LogrosUsuariosRepositorio(context)));

        //Act
        for (var i = 0; i < 25; i++)
        {
            await servicio.Crear(new Comentario
            {
                PublicacionId = publicacion.Id,
                UsuarioId = usuario.Id,
                Contenido = $"Comentario {i}"
            });
        }

        //Assert
        Assert.Equal(25, await context.Comentarios.CountAsync());
        var logros = await context.Logros_Usuarios.Where(logro => logro.UsuarioId == usuario.Id)
            .Select(logro => logro.LogroId).OrderBy(id => id).ToListAsync();
        Assert.Equal(esAutor ? new[] { 12 } : new[] { 2, 12 }, logros);
        Assert.False(await context.Logros_Usuarios.AnyAsync(logro => logro.UsuarioId != usuario.Id));
        notificaciones.Verify(s => s.NotificarComentarioEnPublicacionAsync(
            autor.Id, publicacion.Id, usuario.Id, It.IsAny<CancellationToken>()),
            Times.Exactly(esAutor ? 0 : 25));
    }

    [Fact]
    public async Task QueSePermitaResponderUnaRespuestaSinLimiteDeProfundidad()
    {
        //Arrange
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var servicio = CrearServicio(context);

        //Act
        var raiz = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Raíz", UsuarioId = autor.Id });
        var respuesta = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Respuesta", UsuarioId = lector.Id, ComentarioPadreId = raiz.Id });
        var nieta = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Tercer nivel", UsuarioId = autor.Id, ComentarioPadreId = respuesta.Id });

        //Assert
        Assert.Equal(respuesta.Id, nieta.ComentarioPadreId);
        Assert.Equal(3, await context.Comentarios.CountAsync());
    }

    #endregion

    #region Consultas y Paginación

    [Fact]
    public async Task QueSeEntreguenTresRespuestasInicialesYCursor()
    {
        //Arrange
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var raiz = new Comentario
        {
            Contenido = "Raíz",
            PublicacionId = publicacion.Id,
            UsuarioId = autor.Id,
            FechaCreacion = DateTime.UtcNow.AddHours(-1)
        };
        context.Comentarios.Add(raiz);
        await context.SaveChangesAsync();

        for (var indice = 0; indice < 5; indice++)
        {
            context.Comentarios.Add(new Comentario
            {
                Contenido = $"Respuesta {indice}",
                PublicacionId = publicacion.Id,
                UsuarioId = lector.Id,
                ComentarioPadreId = raiz.Id,
                FechaCreacion = DateTime.UtcNow.AddMinutes(indice)
            });
        }
        await context.SaveChangesAsync();

        //Act
        var pagina = await CrearServicio(context).ObtenerRaices(publicacion.Id, null, 20, lector.Id);
        var comentario = Assert.Single(pagina.Items);

        //Assert
        Assert.Equal(5, comentario.CantidadRespuestas);
        Assert.Equal(3, comentario.Respuestas.Count);
        Assert.NotNull(comentario.SiguienteCursorRespuestas);
    }

    #endregion

    #region Eliminación de Comentarios

    [Fact]
    public async Task QueEliminarConserveElNodoSiTieneRespuestasYOculteIdentidad()
    {
        //Arrange
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var servicio = CrearServicio(context);
        var raiz = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Se elimina", UsuarioId = autor.Id });
        await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Se conserva", UsuarioId = lector.Id, ComentarioPadreId = raiz.Id });

        //Act
        await servicio.Eliminar(publicacion.Id, raiz.Id, autor.Id);
        var eliminado = Assert.Single((await servicio.ObtenerRaices(publicacion.Id, null, 20, autor.Id)).Items);

        //Assert
        Assert.True(eliminado.EstaEliminado);
        Assert.Null(eliminado.Contenido);
        Assert.Null(eliminado.Autor);
        Assert.Single(eliminado.Respuestas);
    }

    [Fact]
    public async Task QueSeImpidaEliminarUnComentarioAjeno()
    {
        //Arrange
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var servicio = CrearServicio(context);
        var comentario = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Propio", UsuarioId = autor.Id });

        //Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            servicio.Eliminar(publicacion.Id, comentario.Id, lector.Id));
    }

    #endregion

    #region Reacciones

    [Fact]
    public async Task QueCambiarYQuitarReaccionMantengaUnaPorUsuario()
    {
        //Arrange
        await using var context = CrearContexto();
        var (publicacion, autor, lector) = await CrearEscenario(context);
        var servicio = CrearServicio(context);
        var comentario = await servicio.Crear(new Comentario { PublicacionId = publicacion.Id, Contenido = "Comentario", UsuarioId = autor.Id });

        //Act - Establecer y cambiar
        await servicio.EstablecerReaccion(publicacion.Id, comentario.Id, lector.Id, "like");
        var actualizado = await servicio.EstablecerReaccion(publicacion.Id, comentario.Id, lector.Id, "surprise");

        //Assert
        Assert.Equal("surprise", actualizado.ReaccionActual);
        Assert.Equal(1, actualizado.Reacciones["surprise"]);

        //Act - Quitar
        var sinReaccion = await servicio.QuitarReaccion(publicacion.Id, comentario.Id, lector.Id);

        //Assert
        Assert.Null(sinReaccion.ReaccionActual);
        Assert.Empty(sinReaccion.Reacciones);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private static ComentarioServicio CrearServicio(ApplicationDbContext context) =>
        new(new ComentarioRepositorio(context), new UsuarioRepositorio(context), Mock.Of<INotificacionServicio>(), Mock.Of<ILogroServicio>());

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

    #endregion
}
