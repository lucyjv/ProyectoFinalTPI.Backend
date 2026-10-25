using ProyectoFinalTPI.Backend.Dtos.Notificacion;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    /// <summary>
    /// Contrato para la entrega de notificaciones en tiempo real.
    /// La capa de Servicio depende de esta abstracción; no sabe nada de SignalR.
    /// La implementación concreta (SignalR, WebPush, etc.) vive en la capa de infraestructura.
    /// </summary>
    public interface INotificacionPush
    {
        /// <summary>
        /// Envía una notificación en tiempo real al usuario indicado.
        /// Si el usuario no está conectado, la implementación descarta el envío
        /// silenciosamente: la notificación ya quedó persistida en BD.
        /// </summary>
        Task EnviarAsync(
            int usuarioDestinatarioId,
            NotificacionDto notificacion,
            CancellationToken cancellationToken = default);
    }
}
