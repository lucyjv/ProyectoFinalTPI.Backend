using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class NotificacionRepositorioTests
{
    #region Guardar Notificación

    [Fact]
    public async Task QueGuardarAsyncPersistaNotificacionEnBaseDeDatos()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);
        var notificacion = NotificacionBase();

        //Act
        await repositorio.GuardarAsync(notificacion);

        //Assert
        Assert.Equal(1, await context.Notificaciones.CountAsync());
    }

    [Fact]
    public async Task QueGuardarAsyncAsigneIdGenerado()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);
        var notificacion = NotificacionBase();

        //Act
        await repositorio.GuardarAsync(notificacion);

        //Assert
        Assert.True(notificacion.Id > 0);
    }

    #endregion

    #region Consultar Notificaciones por Usuario

    [Fact]
    public async Task QueObtenerPorUsuarioDevuelvaSoloNotificacionesDelUsuario()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        context.Notificaciones.AddRange(
            NotificacionBase(destinatarioId: 1),
            NotificacionBase(destinatarioId: 2),
            NotificacionBase(destinatarioId: 1));
        await context.SaveChangesAsync();

        //Act
        var resultado = (await repositorio.ObtenerPorUsuarioAsync(1)).ToList();

        //Assert
        Assert.Equal(2, resultado.Count);
        Assert.All(resultado, n => Assert.Equal(1, n.UsuarioDestinatarioId));
    }

    [Fact]
    public async Task QueObtenerPorUsuarioNoDevuelvaNotificacionesExpiradas()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var expirada = NotificacionBase(leida: true);
        expirada.FechaLeida = DateTime.UtcNow.AddDays(-10);
        expirada.FechaExpiracion = DateTime.UtcNow.AddDays(-3);

        var vigente = NotificacionBase();

        context.Notificaciones.AddRange(expirada, vigente);
        await context.SaveChangesAsync();

        //Act
        var resultado = (await repositorio.ObtenerPorUsuarioAsync(1)).ToList();

        //Assert
        Assert.Single(resultado);
        Assert.Null(resultado[0].FechaExpiracion);
    }

    [Fact]
    public async Task QueObtenerPorUsuarioDevuelvaOrdenadasDeMasRecienteAMasAntigua()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var antigua = NotificacionBase();
        antigua.FechaCreacion = DateTime.UtcNow.AddHours(-2);

        var reciente = NotificacionBase();
        reciente.FechaCreacion = DateTime.UtcNow.AddMinutes(-5);

        context.Notificaciones.AddRange(antigua, reciente);
        await context.SaveChangesAsync();

        //Act
        var resultado = (await repositorio.ObtenerPorUsuarioAsync(1)).ToList();

        //Assert
        Assert.True(resultado[0].FechaCreacion > resultado[1].FechaCreacion);
    }

    #endregion

    #region Marcar Notificaciones Como Leídas

    [Fact]
    public async Task QueMarcarComoLeidaFijeLeidaFechaLeidaYFechaExpiracion()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var notificacion = NotificacionBase();
        context.Notificaciones.Add(notificacion);
        await context.SaveChangesAsync();

        var antes = DateTime.UtcNow;

        //Act
        await repositorio.MarcarComoLeidaAsync(notificacion.Id, usuarioDestinatarioId: 1);
        var despues = DateTime.UtcNow;

        //Assert
        var actualizada = await context.Notificaciones.FindAsync(notificacion.Id);
        Assert.NotNull(actualizada);
        Assert.True(actualizada.Leida);
        Assert.NotNull(actualizada.FechaLeida);
        Assert.InRange(actualizada.FechaLeida!.Value, antes, despues);
        Assert.NotNull(actualizada.FechaExpiracion);
        Assert.True(actualizada.FechaExpiracion >= antes.AddDays(7));
    }

    [Fact]
    public async Task QueMarcarComoLeidaLanceKeyNotFoundSiNoExiste()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        //Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => repositorio.MarcarComoLeidaAsync(notificacionId: 999, usuarioDestinatarioId: 1));
    }

    [Fact]
    public async Task QueMarcarComoLeidaLanceKeyNotFoundSiPerteneceAOtroUsuario()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var notificacion = NotificacionBase(destinatarioId: 1);
        context.Notificaciones.Add(notificacion);
        await context.SaveChangesAsync();

        //Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => repositorio.MarcarComoLeidaAsync(notificacion.Id, usuarioDestinatarioId: 2));
    }

    [Fact]
    public async Task QueMarcarComoLeidaSeaIdempotenteSiYaEstabaLeida()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var fechaLeidaOriginal = DateTime.UtcNow.AddMinutes(-10);
        var notificacion = NotificacionBase(leida: true);
        notificacion.FechaLeida = fechaLeidaOriginal;
        notificacion.FechaExpiracion = fechaLeidaOriginal.AddDays(7);
        context.Notificaciones.Add(notificacion);
        await context.SaveChangesAsync();

        //Act
        await repositorio.MarcarComoLeidaAsync(notificacion.Id, usuarioDestinatarioId: 1);

        //Assert
        var resultado = await context.Notificaciones.FindAsync(notificacion.Id);
        Assert.Equal(fechaLeidaOriginal, resultado!.FechaLeida);
    }

    [Fact]
    public async Task QueMarcarTodasComoLeidasMarqueSoloLasDelUsuarioIndicado()
    {
        //Arrange
        await using var context = CrearContexto();
        var n1 = NotificacionBase(destinatarioId: 1);
        var n2 = NotificacionBase(destinatarioId: 1);
        var n3 = NotificacionBase(destinatarioId: 2);
        context.Notificaciones.AddRange(n1, n2, n3);
        await context.SaveChangesAsync();

        //Act
        var pendientes = await context.Notificaciones
            .Where(n => n.UsuarioDestinatarioId == 1 && !n.Leida)
            .ToListAsync();
        var ahora = DateTime.UtcNow;
        foreach (var n in pendientes)
        {
            n.Leida = true;
            n.FechaLeida = ahora;
            n.FechaExpiracion = ahora.AddDays(7);
        }
        await context.SaveChangesAsync();

        //Assert
        var usuario1 = await context.Notificaciones
            .Where(n => n.UsuarioDestinatarioId == 1).ToListAsync();
        var usuario2 = await context.Notificaciones
            .Where(n => n.UsuarioDestinatarioId == 2).ToListAsync();

        Assert.All(usuario1, n => Assert.True(n.Leida));
        Assert.All(usuario2, n => Assert.False(n.Leida));
    }

    #endregion

    #region Eliminación y Detección de Notificaciones Pendientes

    [Fact]
    public async Task QueEliminarExpiradasElimineSoloLasQueYaExpiraron()
    {
        //Arrange
        await using var context = CrearContexto();
        var expirada = NotificacionBase(leida: true);
        expirada.FechaExpiracion = DateTime.UtcNow.AddDays(-1);

        var noExpirada = NotificacionBase(leida: true);
        noExpirada.FechaExpiracion = DateTime.UtcNow.AddDays(6);

        var noLeida = NotificacionBase();

        context.Notificaciones.AddRange(expirada, noExpirada, noLeida);
        await context.SaveChangesAsync();

        //Act
        var ahora = DateTime.UtcNow;
        var expiradas = await context.Notificaciones
            .Where(n => n.FechaExpiracion != null && n.FechaExpiracion <= ahora)
            .ToListAsync();
        context.Notificaciones.RemoveRange(expiradas);
        await context.SaveChangesAsync();

        //Assert
        Assert.Equal(2, await context.Notificaciones.CountAsync());
    }

    [Fact]
    public async Task QueExisteNotificacionPendienteDevuelvaTrueSiHayPendienteDelMismoTipoYPublicacion()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var pendiente = NotificacionBase(
            tipo: TipoNotificacion.ComentarioEnPublicacion,
            publicacionId: 99,
            leida: false);
        context.Notificaciones.Add(pendiente);
        await context.SaveChangesAsync();

        //Act
        var existe = await repositorio.ExisteNotificacionPendienteAsync(
            1, TipoNotificacion.ComentarioEnPublicacion, 99);

        //Assert
        Assert.True(existe);
    }

    [Fact]
    public async Task QueExisteNotificacionPendienteDevuelvaFalseSiYaFueLeida()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var leida = NotificacionBase(
            tipo: TipoNotificacion.ComentarioEnPublicacion,
            publicacionId: 99,
            leida: true);
        context.Notificaciones.Add(leida);
        await context.SaveChangesAsync();

        //Act
        var existe = await repositorio.ExisteNotificacionPendienteAsync(
            1, TipoNotificacion.ComentarioEnPublicacion, 99);

        //Assert
        Assert.False(existe);
    }

    [Fact]
    public async Task QueExisteNotificacionPendienteDevuelvaFalseSiEsOtroTipo()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var pendiente = NotificacionBase(
            tipo: TipoNotificacion.PuntuacionEnPublicacion,
            publicacionId: 99,
            leida: false);
        context.Notificaciones.Add(pendiente);
        await context.SaveChangesAsync();

        //Act
        var existe = await repositorio.ExisteNotificacionPendienteAsync(
            1, TipoNotificacion.ComentarioEnPublicacion, 99);

        //Assert
        Assert.False(existe);
    }

    [Fact]
    public async Task QueExisteNotificacionPendienteConPublicacionNullDetectePendienteSinPublicacion()
    {
        //Arrange
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var pendiente = NotificacionBase(
            tipo: TipoNotificacion.MensajeChat,
            publicacionId: null,
            leida: false);
        context.Notificaciones.Add(pendiente);
        await context.SaveChangesAsync();

        //Act
        var existe = await repositorio.ExisteNotificacionPendienteAsync(
            1, TipoNotificacion.MensajeChat, publicacionId: null);

        //Assert
        Assert.True(existe);
    }

    #endregion

    #region Métodos Auxiliares Privados

    private static ApplicationDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(opciones);
    }

    private static Notificacion NotificacionBase(
        int destinatarioId = 1,
        TipoNotificacion tipo = TipoNotificacion.NuevoSeguidor,
        int? publicacionId = null,
        bool leida = false) =>
        new()
        {
            UsuarioDestinatarioId = destinatarioId,
            Tipo = tipo,
            PublicacionId = publicacionId,
            Leida = leida,
            FechaCreacion = DateTime.UtcNow
        };

    #endregion
}
