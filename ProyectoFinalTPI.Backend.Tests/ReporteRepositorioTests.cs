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
