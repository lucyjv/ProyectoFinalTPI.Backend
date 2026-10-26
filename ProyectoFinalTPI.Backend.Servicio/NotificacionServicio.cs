using ProyectoFinalTPI.Backend.Dtos.Notificacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    /// <summary>
    /// Lógica de negocio de notificaciones.
    /// Persiste cada notificación y delega la entrega en tiempo real a INotificacionPush.
    /// No conoce detalles de EF Core ni de SignalR.
    /// </summary>
    public class NotificacionServicio : INotificacionServicio
    {
        private readonly INotificacionRepositorio _repositorio;
        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly INotificacionPush _push;

        public NotificacionServicio(
            INotificacionRepositorio repositorio,
            IUsuarioRepositorio usuarioRepositorio,
            INotificacionPush push)
        {
            _repositorio = repositorio;
            _usuarioRepositorio = usuarioRepositorio;
            _push = push;
        }

        /// <inheritdoc/>
        public async Task NotificarNuevoSeguidorAsync(
            int usuarioDestinatarioId, int nuevoSeguidorId,
            CancellationToken cancellationToken = default)
        {
            var notificacion = new Notificacion
            {
                UsuarioDestinatarioId = usuarioDestinatarioId,
                Tipo = TipoNotificacion.NuevoSeguidor,
                EmisorId = nuevoSeguidorId,
                FechaCreacion = DateTime.UtcNow
            };

            await _repositorio.GuardarAsync(notificacion, cancellationToken);
            await EnviarAsync(usuarioDestinatarioId, notificacion, nuevoSeguidorId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task NotificarComentarioEnPublicacionAsync(
            int usuarioDestinatarioId, int publicacionId, int comentadorId,
            CancellationToken cancellationToken = default)
        {
            var yaExiste = await _repositorio.ExisteNotificacionPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.ComentarioEnPublicacion,
                publicacionId,
                cancellationToken);

            if (yaExiste) return;

            var notificacion = new Notificacion
            {
                UsuarioDestinatarioId = usuarioDestinatarioId,
                Tipo = TipoNotificacion.ComentarioEnPublicacion,
                PublicacionId = publicacionId,
                EmisorId = comentadorId,
                FechaCreacion = DateTime.UtcNow
            };

            await _repositorio.GuardarAsync(notificacion, cancellationToken);
            await EnviarAsync(usuarioDestinatarioId, notificacion, comentadorId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task NotificarPuntuacionEnPublicacionAsync(
            int usuarioDestinatarioId, int publicacionId, int puntuadorId,
            CancellationToken cancellationToken = default)
        {
            var yaExiste = await _repositorio.ExisteNotificacionPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.PuntuacionEnPublicacion,
                publicacionId,
                cancellationToken);

            if (yaExiste) return;

            var notificacion = new Notificacion
            {
                UsuarioDestinatarioId = usuarioDestinatarioId,
                Tipo = TipoNotificacion.PuntuacionEnPublicacion,
                PublicacionId = publicacionId,
                EmisorId = puntuadorId,
                FechaCreacion = DateTime.UtcNow
            };

            await _repositorio.GuardarAsync(notificacion, cancellationToken);
            await EnviarAsync(usuarioDestinatarioId, notificacion, puntuadorId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task NotificarAscensoCategoriaAsync(
            int usuarioDestinatarioId,
            CancellationToken cancellationToken = default)
        {
            // PLACEHOLDER: se conectará al sistema de categorías cuando esté listo.
            var notificacion = new Notificacion
            {
                UsuarioDestinatarioId = usuarioDestinatarioId,
                Tipo = TipoNotificacion.AscensoCategoria,
                FechaCreacion = DateTime.UtcNow
            };

            await _repositorio.GuardarAsync(notificacion, cancellationToken);
            await EnviarAsync(usuarioDestinatarioId, notificacion, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task NotificarMensajesCorreoAsync(
            int usuarioDestinatarioId,
            CancellationToken cancellationToken = default)
        {
            // PLACEHOLDER: se conectará al módulo de casilla de correo cuando esté listo.
            var yaExiste = await _repositorio.ExisteNotificacionPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.MensajesCorreo,
                publicacionId: null,
                cancellationToken);

            if (yaExiste) return;

            var notificacion = new Notificacion
            {
                UsuarioDestinatarioId = usuarioDestinatarioId,
                Tipo = TipoNotificacion.MensajesCorreo,
                FechaCreacion = DateTime.UtcNow
            };

            await _repositorio.GuardarAsync(notificacion, cancellationToken);
            await EnviarAsync(usuarioDestinatarioId, notificacion, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task NotificarMensajeChatAsync(
            int usuarioDestinatarioId, int emisorId,
            CancellationToken cancellationToken = default)
        {
            var yaExiste = await _repositorio.ExisteNotificacionPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.MensajeChat,
                publicacionId: null,
                cancellationToken);

            if (yaExiste) return;

            var notificacion = new Notificacion
            {
                UsuarioDestinatarioId = usuarioDestinatarioId,
                Tipo = TipoNotificacion.MensajeChat,
                EmisorId = emisorId,
                FechaCreacion = DateTime.UtcNow
            };

            await _repositorio.GuardarAsync(notificacion, cancellationToken);
            await EnviarAsync(usuarioDestinatarioId, notificacion, emisorId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<NotificacionDto>> ObtenerNotificacionesAsync(
            int usuarioId, CancellationToken cancellationToken = default)
        {
            var notificaciones = await _repositorio.ObtenerPorUsuarioAsync(
                usuarioId, cancellationToken);

            return notificaciones.Select(MapearADto);
        }

        /// <inheritdoc/>
        public async Task MarcarComoLeidaAsync(
            int notificacionId, int usuarioId,
            CancellationToken cancellationToken = default)
        {
            await _repositorio.MarcarComoLeidaAsync(notificacionId, usuarioId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task MarcarTodasComoLeidasAsync(
            int usuarioId, CancellationToken cancellationToken = default)
        {
            await _repositorio.MarcarTodasComoLeidasAsync(usuarioId, cancellationToken);
        }

        // ── Métodos privados ─────────────────────────────────────────────────

        /// <summary>
        /// Resuelve el username del emisor y delega la entrega en tiempo real a INotificacionPush.
        /// </summary>
        private async Task EnviarAsync(
            int usuarioDestinatarioId,
            Notificacion notificacion,
            int? emisorId,
            CancellationToken cancellationToken)
        {
            string? emisorUsername = null;
            if (emisorId.HasValue)
            {
                var emisor = await _usuarioRepositorio.ObtenerPorIdAsync(emisorId.Value);
                emisorUsername = emisor?.Username;
            }

            await _push.EnviarAsync(
                usuarioDestinatarioId,
                MapearADto(notificacion, emisorUsername),
                cancellationToken);
        }

        private static NotificacionDto MapearADto(Notificacion notificacion) =>
            MapearADto(notificacion, notificacion.Emisor?.Username);

        private static NotificacionDto MapearADto(
            Notificacion notificacion, string? emisorUsername) =>
            new()
            {
                Id = notificacion.Id,
                Tipo = notificacion.Tipo,
                Leida = notificacion.Leida,
                FechaCreacion = notificacion.FechaCreacion,
                PublicacionId = notificacion.PublicacionId,
                EmisorId = notificacion.EmisorId,
                EmisorUsername = emisorUsername
            };
    }
}
