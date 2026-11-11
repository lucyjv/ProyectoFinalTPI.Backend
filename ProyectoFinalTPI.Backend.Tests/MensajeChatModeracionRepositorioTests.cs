using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class MensajeChatModeracionRepositorioTests
{
    [Fact]
    public async Task QueLosMensajesEliminadosPorModeracionNoAparezcanEnChat()
    {
        await using var context = CrearContexto();
        var emisor = Usuario("emisor", "emisor@test.com");
        var receptor = Usuario("receptor", "receptor@test.com");
        context.AddRange(emisor, receptor);
        await context.SaveChangesAsync();
        context.MensajesChat.AddRange(
            Mensaje(emisor, receptor, "visible", false),
            Mensaje(emisor, receptor, "moderado", true));
        await context.SaveChangesAsync();
        var repositorio = new MensajeChatRepositorio(context);

        var historial = await repositorio.ObtenerHistorialAsync(emisor.Id, receptor.Id);
        var conversaciones = await repositorio.ObtenerConversacionesActivasAsync(emisor.Id);

        Assert.Single(historial);
        Assert.Equal("visible", historial.Single().Contenido);
        Assert.Single(conversaciones);
        Assert.Equal("visible", conversaciones.Single().Contenido);
    }

    private static MensajeChat Mensaje(
        Usuario emisor, Usuario receptor, string contenido, bool eliminado) => new()
    {
        Emisor = emisor,
        Destinatario = receptor,
        Contenido = contenido,
        EstaEliminado = eliminado,
        FechaEnvio = DateTime.UtcNow,
        FechaExpiracion = DateTime.UtcNow.AddDays(1)
    };

    private static Usuario Usuario(string username, string email) => new()
    {
        Username = username,
        Email = email,
        Contraseña = "hash"
    };

    private static ApplicationDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
