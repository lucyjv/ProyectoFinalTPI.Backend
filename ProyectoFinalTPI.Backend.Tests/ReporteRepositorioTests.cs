using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ReporteRepositorioTests
{
    [Fact]
    public async Task QueObtenerObjetivoDevuelvaContextoSoloDeContenidoVisible()
    {
        await using var context = CrearContexto();
        var autor = new Usuario_Personal { Username = "autor", Email = "autor@example.test", Contraseña = "hash" };
        var publicacion = new Publicacion { Titulo = "Viaje", Descripcion = "Un recuerdo", Usuario = autor };
        var comentario = new Comentario { Usuario = autor, Publicacion = publicacion, Contenido = "Comentario reportado" };
        context.Add(comentario);
        await context.SaveChangesAsync();
        var repositorio = new ReporteRepositorio(context);

        var objetivoPublicacion = await repositorio.ObtenerObjetivo(TipoContenidoReporte.Publicacion, publicacion.Id, 123);
        var objetivoComentario = await repositorio.ObtenerObjetivo(TipoContenidoReporte.Comentario, comentario.Id, 123);
        var objetivoPerfil = await repositorio.ObtenerObjetivo(TipoContenidoReporte.Perfil, autor.Id, 123);

        Assert.NotNull(objetivoPublicacion);
        Assert.Equal(autor.Id, objetivoPublicacion.UsuarioReportadoId);
        Assert.Equal("Viaje\nUn recuerdo", objetivoPublicacion.ContenidoSnapshot);
        Assert.NotNull(objetivoComentario);
        Assert.Equal(publicacion.Id, objetivoComentario.ContextoId);
        Assert.Equal("Viaje", objetivoComentario.Contexto);
        Assert.Equal("Comentario reportado", objetivoComentario.ContenidoSnapshot);
        Assert.NotNull(objetivoPerfil);
        Assert.Equal("@autor", objetivoPerfil.ContenidoSnapshot);
        Assert.Equal("autor@example.test", objetivoPerfil.Contexto);

        publicacion.EstaOculto = true;
        comentario.EstaEliminado = true;
        autor.EstaEliminado = true;
        await context.SaveChangesAsync();
        Assert.Null(await repositorio.ObtenerObjetivo(TipoContenidoReporte.Publicacion, publicacion.Id, 123));
        Assert.Null(await repositorio.ObtenerObjetivo(TipoContenidoReporte.Comentario, comentario.Id, 123));
        Assert.Null(await repositorio.ObtenerObjetivo(TipoContenidoReporte.Perfil, autor.Id, 123));
    }

    [Fact]
    public async Task QueLosFiltrosCombinenTipoMotivoYTextoYDetectenSoloReportesActivos()
    {
        await using var context = CrearContexto();
        var autor = Usuario("autor", "autor@example.test");
        var reportante = Usuario("reportante", "reportante@example.test");
        var activo = Reporte(reportante, autor, 50);
        activo.ContenidoSnapshot = "Viaje al sur";
        activo.Estado = EstadoReporte.EnRevision;
        var otroTipo = Reporte(reportante, autor, 50);
        otroTipo.TipoContenido = TipoContenidoReporte.Comentario;
        otroTipo.ContenidoSnapshot = activo.ContenidoSnapshot;
        var otroMotivo = Reporte(reportante, autor, 51);
        otroMotivo.Motivo = MotivoReporte.Acoso;
        otroMotivo.ContenidoSnapshot = activo.ContenidoSnapshot;
        var cerrado = Reporte(reportante, autor, 52);
        cerrado.Estado = EstadoReporte.Resuelto;
        var repositorio = new ReporteRepositorio(context);
        await repositorio.Agregar(activo);
        await repositorio.Agregar(otroTipo);
        await repositorio.Agregar(otroMotivo);
        await repositorio.Agregar(cerrado);
        context.ChangeTracker.Clear();

        var pagina = await repositorio.Listar(new FiltroReportesAdminDto
        {
            TipoContenido = TipoContenidoReporte.Publicacion, Motivo = MotivoReporte.Spam, Estado = null, Texto = "  VIAJE  "
        });

        Assert.Equal(activo.Id, Assert.Single(pagina.Items).Id);
        Assert.Equal(1, pagina.TotalItems);
        Assert.NotNull(await repositorio.Obtener(activo.Id));
        Assert.True(await repositorio.ExisteActivo(reportante.Id, TipoContenidoReporte.Publicacion, 50));
        Assert.False(await repositorio.ExisteActivo(reportante.Id, TipoContenidoReporte.Publicacion, 52));
        Assert.False(await repositorio.ExisteActivo(autor.Id, TipoContenidoReporte.Publicacion, 50));
    }

    [Fact]
    public async Task QueEliminarMensajeConserveLaFechaEnUnSegundoIntento()
    {
        await using var context = CrearContexto();
        var mensaje = new MensajeChat
        {
            Emisor = Usuario("emisor", "emisor@example.test"),
            Destinatario = Usuario("destinatario", "destinatario@example.test"),
            Contenido = "Mensaje reportado",
            FechaExpiracion = DateTime.UtcNow.AddDays(1)
        };
        context.MensajesChat.Add(mensaje);
        await context.SaveChangesAsync();
        var repositorio = new ReporteRepositorio(context);

        await repositorio.EliminarMensajeChat(mensaje.Id);
        var eliminado = await context.MensajesChat.AsNoTracking().SingleAsync();
        Assert.True(eliminado.EstaEliminado);
        Assert.NotNull(eliminado.FechaEliminacion);

        await repositorio.EliminarMensajeChat(mensaje.Id);
        Assert.Equal(eliminado.FechaEliminacion, (await context.MensajesChat.AsNoTracking().SingleAsync()).FechaEliminacion);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repositorio.EliminarMensajeChat(mensaje.Id + 1));
    }

    [Fact]
    public async Task QueMensajePrivadoSoloPuedaReportarloUnParticipante()
    {
        await using var context = CrearContexto();
        var emisor = Usuario("emisor", "emisor@test.com");
        var receptor = Usuario("receptor", "receptor@test.com");
        var tercero = Usuario("tercero", "tercero@test.com");
        var mensaje = new MensajeChat
        {
            Emisor = emisor, Destinatario = receptor, Contenido = "Mensaje reportable",
            FechaEnvio = DateTime.UtcNow, FechaExpiracion = DateTime.UtcNow.AddDays(1)
        };
        context.AddRange(emisor, receptor, tercero, mensaje);
        await context.SaveChangesAsync();
        var repositorio = new ReporteRepositorio(context);

        var permitido = await repositorio.ObtenerObjetivo(
            TipoContenidoReporte.MensajeChat, mensaje.Id, receptor.Id);
        var denegado = await repositorio.ObtenerObjetivo(
            TipoContenidoReporte.MensajeChat, mensaje.Id, tercero.Id);

        Assert.Equal(emisor.Id, permitido?.UsuarioReportadoId);
        Assert.Equal("Mensaje reportable", permitido?.ContenidoSnapshot);
        Assert.Null(denegado);
    }

    [Fact]
    public async Task QueListePaginadoYCuenteReportesCoincidentes()
    {
        await using var context = CrearContexto();
        var autor = Usuario("autor", "autor@test.com");
        var reportanteA = Usuario("reporta-a", "a@test.com");
        var reportanteB = Usuario("reporta-b", "b@test.com");
        context.AddRange(autor, reportanteA, reportanteB);
        await context.SaveChangesAsync();
        context.Reportes.AddRange(
            Reporte(reportanteA, autor, 50), Reporte(reportanteB, autor, 50));
        await context.SaveChangesAsync();
        var repositorio = new ReporteRepositorio(context);

        var pagina = await repositorio.Listar(new FiltroReportesAdminDto
        {
            Pagina = 1, Tamanio = 1, Estado = EstadoReporte.Pendiente
        });

        Assert.Equal(2, pagina.TotalItems);
        Assert.Single(pagina.Items);
        Assert.Equal(2, pagina.Items[0].CantidadCoincidentes);
    }

    [Fact]
    public async Task QueResolverApliqueDecisionATodosLosReportesDelContenido()
    {
        await using var context = CrearContexto();
        var admin = Usuario("admin", "admin@test.com");
        var autor = Usuario("autor", "autor@test.com");
        var reportanteA = Usuario("reporta-a", "a@test.com");
        var reportanteB = Usuario("reporta-b", "b@test.com");
        context.AddRange(admin, autor, reportanteA, reportanteB);
        await context.SaveChangesAsync();
        var principal = Reporte(reportanteA, autor, 22);
        context.Reportes.AddRange(principal, Reporte(reportanteB, autor, 22));
        await context.SaveChangesAsync();

        await new ReporteRepositorio(context).ResolverRelacionados(
            principal, admin.Id, AccionReporte.Desestimar, "No infringe las normas");

        var reportes = await context.Reportes.AsNoTracking().ToListAsync();
        Assert.All(reportes, r => Assert.Equal(EstadoReporte.Desestimado, r.Estado));
        Assert.All(reportes, r => Assert.Equal(admin.Id, r.AdministradorId));
    }

    private static Reporte Reporte(Usuario reportante, Usuario autor, int contenidoId) => new()
    {
        Reportante = reportante,
        UsuarioReportado = autor,
        TipoContenido = TipoContenidoReporte.Publicacion,
        ContenidoId = contenidoId,
        Motivo = MotivoReporte.Spam,
        ContenidoSnapshot = "Contenido"
    };

    private static Usuario Usuario(string username, string email) => new()
    {
        Username = username, Email = email, Contraseña = "hash"
    };

    private static ApplicationDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
