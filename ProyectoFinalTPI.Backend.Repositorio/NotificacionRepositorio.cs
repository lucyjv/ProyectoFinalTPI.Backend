using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio
{
    /// <summary>
    /// Acceso a datos para las notificaciones, usando EF Core sobre PostgreSQL.
    /// </summary>
    public class NotificacionRepositorio : INotificacionRepositorio
    {
        private readonly ApplicationDbContext _context;

        public NotificacionRepositorio(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task GuardarAsync(
            Notificacion notificacion, CancellationToken cancellationToken = default)
        {
            _context.Notificaciones.Add(notificacion);
            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Notificacion>> ObtenerPorUsuarioAsync(
            int usuarioDestinatarioId, CancellationToken cancellationToken = default)
        {
            var ahora = DateTime.UtcNow;

            return await _context.Notificaciones
                .Include(n => n.Emisor)
                .Where(n =>
                    n.UsuarioDestinatarioId == usuarioDestinatarioId &&
                    (n.FechaExpiracion == null || n.FechaExpiracion > ahora))
                .OrderByDescending(n => n.FechaCreacion)
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task MarcarComoLeidaAsync(
            int notificacionId, int usuarioDestinatarioId,
            CancellationToken cancellationToken = default)
        {
            var notificacion = await _context.Notificaciones
                .FirstOrDefaultAsync(
                    n => n.Id == notificacionId &&
                         n.UsuarioDestinatarioId == usuarioDestinatarioId,
                    cancellationToken)
                ?? throw new KeyNotFoundException(
                    "La notificación no existe o no pertenece al usuario indicado.");

            if (notificacion.Leida) return;

            var ahora = DateTime.UtcNow;
            notificacion.Leida = true;
            notificacion.FechaLeida = ahora;
            notificacion.FechaExpiracion = ahora.AddDays(7);

            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task MarcarTodasComoLeidasAsync(
            int usuarioDestinatarioId, CancellationToken cancellationToken = default)
        {
            var ahora = DateTime.UtcNow;

            await _context.Notificaciones
                .Where(n =>
                    n.UsuarioDestinatarioId == usuarioDestinatarioId &&
                    !n.Leida)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(n => n.Leida, true)
                        .SetProperty(n => n.FechaLeida, ahora)
                        .SetProperty(n => n.FechaExpiracion, ahora.AddDays(7)),
                    cancellationToken);
        }

        /// <inheritdoc/>
        public async Task EliminarExpiradasAsync(CancellationToken cancellationToken = default)
        {
            var ahora = DateTime.UtcNow;

            await _context.Notificaciones
                .Where(n => n.FechaExpiracion != null && n.FechaExpiracion <= ahora)
                .ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> ExisteNotificacionPendienteAsync(
            int usuarioDestinatarioId, TipoNotificacion tipo, int? publicacionId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Notificaciones
                .AnyAsync(
                    n => n.UsuarioDestinatarioId == usuarioDestinatarioId &&
                         n.Tipo == tipo &&
                         !n.Leida &&
                         n.PublicacionId == publicacionId,
                    cancellationToken);
        }
    }
}
