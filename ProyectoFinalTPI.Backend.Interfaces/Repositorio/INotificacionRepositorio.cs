using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio
{
    /// <summary>
    /// Contrato de acceso a datos para las notificaciones.
    /// Solo persiste, consulta y limpia. No sabe nada de SignalR ni de DTOs.
    /// </summary>
    public interface INotificacionRepositorio
    {
        /// <summary>
        /// Persiste una nueva notificación en la base de datos.
        /// </summary>
        Task GuardarAsync(Notificacion notificacion, CancellationToken cancellationToken = default);

        /// <summary>
        /// Devuelve todas las notificaciones no expiradas del usuario,
        /// ordenadas de más reciente a más antigua.
        /// </summary>
        Task<IEnumerable<Notificacion>> ObtenerPorUsuarioAsync(
            int usuarioDestinatarioId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marca como leída una notificación específica del usuario.
        /// Fija <c>FechaLeida</c> y <c>FechaExpiracion</c> (= FechaLeida + 7 días).
        /// Lanza <see cref="KeyNotFoundException"/> si la notificación no existe
        /// o no pertenece al usuario indicado.
        /// </summary>
        Task MarcarComoLeidaAsync(
            int notificacionId, int usuarioDestinatarioId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marca como leídas todas las notificaciones no leídas del usuario.
        /// Fija <c>FechaLeida</c> y <c>FechaExpiracion</c> en cada una.
        /// </summary>
        Task MarcarTodasComoLeidasAsync(
            int usuarioDestinatarioId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Elimina todas las notificaciones cuya <c>FechaExpiracion</c> ya pasó.
        /// Llamado por el background service de limpieza.
        /// </summary>
        Task EliminarExpiradasAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Indica si ya existe una notificación no leída del tipo indicado
        /// para el destinatario dado, filtrando opcionalmente por publicación.
        /// <para>
        /// Para tipos asociados a una publicación (ComentarioEnPublicacion,
        /// PuntuacionEnPublicacion), pasar el id correspondiente en
        /// <paramref name="publicacionId"/> para aplicar la regla
        /// "una sola notificación por publicación".
        /// </para>
        /// <para>
        /// Para tipos sin publicación (MensajeChat, MensajesCorreo,
        /// AscensoCategoria), pasar <c>null</c> para aplicar la regla
        /// "una sola notificación pendiente del tipo por usuario".
        /// </para>
        /// </summary>
        Task<bool> ExisteNotificacionPendienteAsync(
            int usuarioDestinatarioId, TipoNotificacion tipo, int? publicacionId,
            CancellationToken cancellationToken = default);
    }
}
