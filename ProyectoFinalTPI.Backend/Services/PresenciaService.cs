using System.Collections.Concurrent;

namespace ProyectoFinalTPI.Backend.Services
{
    /// <summary>
    /// Mantiene en memoria qué usuarios están conectados al Hub de chat
    /// y cuál es su ConnectionId de SignalR actual.
    ///
    /// Se registra como Singleton porque debe sobrevivir a todas las peticiones.
    /// ConcurrentDictionary garantiza acceso seguro desde múltiples threads
    /// simultáneos sin necesidad de locks explícitos.
    /// </summary>
    public class PresenciaService
    {
        // Clave: UsuarioId | Valor: ConnectionId de SignalR
        private readonly ConcurrentDictionary<int, string> _conectados = new();

        /// <summary>
        /// Registra al usuario como conectado con su ConnectionId de SignalR.
        /// Si el usuario tenía una conexión anterior (por ejemplo, abrió otra pestaña),
        /// se reemplaza por la nueva.
        /// </summary>
        public void Conectar(int usuarioId, string connectionId)
            => _conectados[usuarioId] = connectionId;

        /// <summary>
        /// Elimina al usuario del registro de conectados.
        /// Llamado cuando SignalR detecta que el cliente cerró la conexión.
        /// </summary>
        public void Desconectar(int usuarioId)
            => _conectados.TryRemove(usuarioId, out _);

        /// <summary>
        /// Devuelve el ConnectionId del usuario si está en línea, o null si no lo está.
        /// </summary>
        public string? ObtenerConnectionId(int usuarioId)
            => _conectados.TryGetValue(usuarioId, out var connectionId) ? connectionId : null;

        /// <summary>
        /// Indica si el usuario tiene una conexión WebSocket activa.
        /// </summary>
        public bool EstaEnLinea(int usuarioId)
            => _conectados.ContainsKey(usuarioId);
    }
}
