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

        #region Operaciones públicas

        /// <inheritdoc/>
        public Task NotificarNuevoSeguidorAsync(
            int usuarioDestinatarioId, int nuevoSeguidorId,
            CancellationToken cancellationToken = default) =>
            GuardarYEnviarAsync(
                usuarioDestinatarioId,
                TipoNotificacion.NuevoSeguidor,
                null, nuevoSeguidorId, cancellationToken);

        /// <inheritdoc/>
        public Task NotificarComentarioEnPublicacionAsync(
            int usuarioDestinatarioId, int publicacionId, int comentadorId,
            CancellationToken cancellationToken = default) =>
            NotificarSiNoHayPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.ComentarioEnPublicacion,
                publicacionId, comentadorId, cancellationToken);

        /// <inheritdoc/>
        public Task NotificarPuntuacionEnPublicacionAsync(
            int usuarioDestinatarioId, int publicacionId, int puntuadorId, int? puntaje = null,
            CancellationToken cancellationToken = default) =>
            NotificarSiNoHayPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.PuntuacionEnPublicacion,
                publicacionId, puntuadorId, cancellationToken, puntaje);

        /// <inheritdoc/>
        public Task NotificarMeGustaPublicacionAsync(
            int usuarioDestinatarioId, int publicacionId, int usuarioQueDioLikeId,
            CancellationToken cancellationToken = default) =>
            NotificarSiNoHayPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.MeGustaPublicacion,
                publicacionId, usuarioQueDioLikeId, cancellationToken);

        /// <inheritdoc/>
        public Task NotificarCompartirPublicacionAsync(
            int usuarioDestinatarioId, int publicacionId, int reposteadorId,
            CancellationToken cancellationToken = default) =>
            NotificarSiNoHayPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.CompartirPublicacion,
                publicacionId, reposteadorId, cancellationToken);

        /// <inheritdoc/>
        public Task NotificarAscensoCategoriaAsync(
            int usuarioDestinatarioId,
            CancellationToken cancellationToken = default)
        {
            // PLACEHOLDER: se conectará al sistema de categorías cuando esté listo.
            return GuardarYEnviarAsync(
                usuarioDestinatarioId,
                TipoNotificacion.AscensoCategoria,
                null, null, cancellationToken);
        }

        /// <inheritdoc/>
        public Task NotificarMensajesCorreoAsync(
            int usuarioDestinatarioId,
            CancellationToken cancellationToken = default)
        {
            // PLACEHOLDER: se conectará al módulo de casilla de correo cuando esté listo.
            return NotificarSiNoHayPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.MensajesCorreo,
                null, null, cancellationToken);
        }

        /// <inheritdoc/>
        public Task NotificarMensajeChatAsync(
            int usuarioDestinatarioId, int emisorId,
            CancellationToken cancellationToken = default) =>
            NotificarSiNoHayPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.MensajeChat,
                null, emisorId, cancellationToken);

        public Task NotificarAdministrativaAsync(
            int usuarioDestinatarioId, int? publicacionId = null,
            CancellationToken cancellationToken = default) =>
            GuardarYEnviarAsync(
                usuarioDestinatarioId,
                TipoNotificacion.Administrativa,
                publicacionId,
                null,
                cancellationToken);

        /// <inheritdoc/>
        public Task NotificarEtiquetadoEnPublicacionAsync(
            int usuarioDestinatarioId, int publicacionId, int emisorId,
            CancellationToken cancellationToken = default)
        {
            if (usuarioDestinatarioId == emisorId)
                return Task.CompletedTask;

            return NotificarSiNoHayPendienteAsync(
                usuarioDestinatarioId,
                TipoNotificacion.EtiquetadoEnPublicacion,
                publicacionId,
                emisorId,
                cancellationToken);
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

        #endregion

        #region Métodos privados y validaciones

        /// <summary>
        /// Resuelve el username del emisor y delega la entrega en tiempo real a INotificacionPush.
        /// </summary>
        /// 

        private async Task NotificarSiNoHayPendienteAsync(
                int destinatarioId, TipoNotificacion tipo,
                int? publicacionId, int? emisorId,
                CancellationToken cancellationToken = default,
                int? puntaje = null)
        {
            if (await _repositorio.ExisteNotificacionPendienteAsync(
                destinatarioId, tipo, publicacionId, cancellationToken))
                return;

            await GuardarYEnviarAsync(
                destinatarioId, tipo, publicacionId,
                emisorId, cancellationToken, puntaje);
        }
        private async Task GuardarYEnviarAsync(
            int destinatarioId, TipoNotificacion tipo,
            int? publicacionId, int? emisorId,
            CancellationToken cancellationToken = default,
            int? puntaje = null)
        {
            var notificacion = CrearNotificacion(
                destinatarioId, tipo, publicacionId, emisorId, puntaje);
            await _repositorio.GuardarAsync(
                notificacion, cancellationToken);
            await EnviarAsync(
                destinatarioId, notificacion,
                emisorId, cancellationToken);
        }
        private static Notificacion CrearNotificacion(
            int destinatarioId, TipoNotificacion tipo,
            int? publicacionId, int? emisorId, int? puntaje = null) =>
            new()
            {
                UsuarioDestinatarioId = destinatarioId,
                Tipo = tipo,
                PublicacionId = publicacionId,
                EmisorId = emisorId,
                Puntaje = puntaje,
                FechaCreacion = DateTime.UtcNow
            };
        private async Task EnviarAsync(
            int usuarioDestinatarioId,
            Notificacion notificacion,
            int? emisorId,
            CancellationToken cancellationToken)
        {
            var emisorUsername = await ObtenerUsernameEmisorAsync(emisorId);
            await _push.EnviarAsync(
                usuarioDestinatarioId,
                MapearADto(notificacion, emisorUsername),
                cancellationToken);
        }
        private async Task<string?> ObtenerUsernameEmisorAsync(int? emisorId)
        {
            if (!emisorId.HasValue) return null;
            var emisor = await _usuarioRepositorio.ObtenerPorIdAsync(
                emisorId.Value);
            return emisor?.Username;
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
                EmisorUsername = emisorUsername,
                Puntaje = notificacion.Puntaje
            };

        #endregion
    }
}
