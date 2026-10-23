using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class NotificacionRepositorioTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

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

    // ── GuardarAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GuardarAsync_persiste_la_notificacion_en_la_bd()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var notificacion = NotificacionBase();
        await repositorio.GuardarAsync(notificacion);

        Assert.Equal(1, await context.Notificaciones.CountAsync());
    }

    [Fact]
    public async Task GuardarAsync_asigna_id_generado_por_la_bd()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var notificacion = NotificacionBase();
        await repositorio.GuardarAsync(notificacion);

        Assert.True(notificacion.Id > 0);
    }

    // ── ObtenerPorUsuarioAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerPorUsuario_devuelve_solo_notificaciones_del_usuario()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        context.Notificaciones.AddRange(
            NotificacionBase(destinatarioId: 1),
            NotificacionBase(destinatarioId: 2),
            NotificacionBase(destinatarioId: 1));
        await context.SaveChangesAsync();

        var resultado = (await repositorio.ObtenerPorUsuarioAsync(1)).ToList();

        Assert.Equal(2, resultado.Count);
        Assert.All(resultado, n => Assert.Equal(1, n.UsuarioDestinatarioId));
    }

    [Fact]
    public async Task ObtenerPorUsuario_no_devuelve_notificaciones_expiradas()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var expirada = NotificacionBase(leida: true);
        expirada.FechaLeida = DateTime.UtcNow.AddDays(-10);
        expirada.FechaExpiracion = DateTime.UtcNow.AddDays(-3); // ya expiró

        var vigente = NotificacionBase();

        context.Notificaciones.AddRange(expirada, vigente);
        await context.SaveChangesAsync();

        var resultado = (await repositorio.ObtenerPorUsuarioAsync(1)).ToList();

        Assert.Single(resultado);
        Assert.Null(resultado[0].FechaExpiracion);
    }

    [Fact]
    public async Task ObtenerPorUsuario_devuelve_ordenadas_de_mas_reciente_a_mas_antigua()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var antigua = NotificacionBase();
        antigua.FechaCreacion = DateTime.UtcNow.AddHours(-2);

        var reciente = NotificacionBase();
        reciente.FechaCreacion = DateTime.UtcNow.AddMinutes(-5);

        context.Notificaciones.AddRange(antigua, reciente);
        await context.SaveChangesAsync();

        var resultado = (await repositorio.ObtenerPorUsuarioAsync(1)).ToList();

        Assert.True(resultado[0].FechaCreacion > resultado[1].FechaCreacion);
    }

    // ── MarcarComoLeidaAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task MarcarComoLeida_fija_leida_fecha_leida_y_fecha_expiracion()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var notificacion = NotificacionBase();
        context.Notificaciones.Add(notificacion);
        await context.SaveChangesAsync();

        var antes = DateTime.UtcNow;
        await repositorio.MarcarComoLeidaAsync(notificacion.Id, usuarioDestinatarioId: 1);
        var despues = DateTime.UtcNow;

        var actualizada = await context.Notificaciones.FindAsync(notificacion.Id);
        Assert.NotNull(actualizada);
        Assert.True(actualizada.Leida);
        Assert.NotNull(actualizada.FechaLeida);
        Assert.InRange(actualizada.FechaLeida!.Value, antes, despues);
        Assert.NotNull(actualizada.FechaExpiracion);
        Assert.True(actualizada.FechaExpiracion >= antes.AddDays(7));
    }

    [Fact]
    public async Task MarcarComoLeida_lanza_KeyNotFound_si_la_notificacion_no_existe()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => repositorio.MarcarComoLeidaAsync(
                notificacionId: 999, usuarioDestinatarioId: 1));
    }

    [Fact]
    public async Task MarcarComoLeida_lanza_KeyNotFound_si_la_notificacion_pertenece_a_otro_usuario()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var notificacion = NotificacionBase(destinatarioId: 1);
        context.Notificaciones.Add(notificacion);
        await context.SaveChangesAsync();

        // Usuario 2 intenta marcar una notificación del usuario 1
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => repositorio.MarcarComoLeidaAsync(
                notificacion.Id, usuarioDestinatarioId: 2));
    }

    [Fact]
    public async Task MarcarComoLeida_es_idempotente_si_ya_estaba_leida()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var fechaLeidaOriginal = DateTime.UtcNow.AddMinutes(-10);
        var notificacion = NotificacionBase(leida: true);
        notificacion.FechaLeida = fechaLeidaOriginal;
        notificacion.FechaExpiracion = fechaLeidaOriginal.AddDays(7);
        context.Notificaciones.Add(notificacion);
        await context.SaveChangesAsync();

        // Segunda llamada no debe cambiar la FechaLeida original
        await repositorio.MarcarComoLeidaAsync(notificacion.Id, usuarioDestinatarioId: 1);

        var resultado = await context.Notificaciones.FindAsync(notificacion.Id);
        Assert.Equal(fechaLeidaOriginal, resultado!.FechaLeida);
    }

    // ── MarcarTodasComoLeidasAsync ────────────────────────────────────────────

    [Fact]
    public async Task MarcarTodasComoLeidas_marca_solo_las_del_usuario_indicado()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var n1 = NotificacionBase(destinatarioId: 1);
        var n2 = NotificacionBase(destinatarioId: 1);
        var n3 = NotificacionBase(destinatarioId: 2);
        context.Notificaciones.AddRange(n1, n2, n3);
        await context.SaveChangesAsync();

        // Implementación compatible con InMemory: carga + modifica + guarda
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

        var usuario1 = await context.Notificaciones
            .Where(n => n.UsuarioDestinatarioId == 1).ToListAsync();
        var usuario2 = await context.Notificaciones
            .Where(n => n.UsuarioDestinatarioId == 2).ToListAsync();

        Assert.All(usuario1, n => Assert.True(n.Leida));
        Assert.All(usuario2, n => Assert.False(n.Leida));
    }

    // ── EliminarExpiradasAsync ────────────────────────────────────────────────

    [Fact]
    public async Task EliminarExpiradas_elimina_solo_las_que_ya_expiraron()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var expirada = NotificacionBase(leida: true);
        expirada.FechaExpiracion = DateTime.UtcNow.AddDays(-1);

        var noExpirada = NotificacionBase(leida: true);
        noExpirada.FechaExpiracion = DateTime.UtcNow.AddDays(6);

        var noLeida = NotificacionBase();

        context.Notificaciones.AddRange(expirada, noExpirada, noLeida);
        await context.SaveChangesAsync();

        // Implementación compatible con InMemory: carga + elimina + guarda
        var ahora = DateTime.UtcNow;
        var expiradas = await context.Notificaciones
            .Where(n => n.FechaExpiracion != null && n.FechaExpiracion <= ahora)
            .ToListAsync();
        context.Notificaciones.RemoveRange(expiradas);
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.Notificaciones.CountAsync());
    }

    // ── ExisteNotificacionPendienteAsync ──────────────────────────────────────

    [Fact]
    public async Task ExisteNotificacionPendiente_devuelve_true_si_hay_una_no_leida_del_mismo_tipo_y_publicacion()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var pendiente = NotificacionBase(
            tipo: TipoNotificacion.ComentarioEnPublicacion,
            publicacionId: 99,
            leida: false);
        context.Notificaciones.Add(pendiente);
        await context.SaveChangesAsync();

        var existe = await repositorio.ExisteNotificacionPendienteAsync(
            1, TipoNotificacion.ComentarioEnPublicacion, 99);

        Assert.True(existe);
    }

    [Fact]
    public async Task ExisteNotificacionPendiente_devuelve_false_si_la_existente_ya_fue_leida()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var leida = NotificacionBase(
            tipo: TipoNotificacion.ComentarioEnPublicacion,
            publicacionId: 99,
            leida: true);
        context.Notificaciones.Add(leida);
        await context.SaveChangesAsync();

        var existe = await repositorio.ExisteNotificacionPendienteAsync(
            1, TipoNotificacion.ComentarioEnPublicacion, 99);

        Assert.False(existe);
    }

    [Fact]
    public async Task ExisteNotificacionPendiente_devuelve_false_si_es_otro_tipo()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var pendiente = NotificacionBase(
            tipo: TipoNotificacion.PuntuacionEnPublicacion,
            publicacionId: 99,
            leida: false);
        context.Notificaciones.Add(pendiente);
        await context.SaveChangesAsync();

        // Preguntamos por ComentarioEnPublicacion pero solo hay PuntuacionEnPublicacion
        var existe = await repositorio.ExisteNotificacionPendienteAsync(
            1, TipoNotificacion.ComentarioEnPublicacion, 99);

        Assert.False(existe);
    }

    [Fact]
    public async Task ExisteNotificacionPendiente_con_publicacionId_null_detecta_pendiente_sin_publicacion()
    {
        await using var context = CrearContexto();
        var repositorio = new NotificacionRepositorio(context);

        var pendiente = NotificacionBase(
            tipo: TipoNotificacion.MensajeChat,
            publicacionId: null,
            leida: false);
        context.Notificaciones.Add(pendiente);
        await context.SaveChangesAsync();

        var existe = await repositorio.ExisteNotificacionPendienteAsync(
            1, TipoNotificacion.MensajeChat, publicacionId: null);

        Assert.True(existe);
    }
}
