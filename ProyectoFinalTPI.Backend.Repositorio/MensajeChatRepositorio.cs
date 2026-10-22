using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio
{
    /// <summary>
    /// Acceso a datos para los mensajes de chat, usando EF Core sobre PostgreSQL.
    /// </summary>
    public class MensajeChatRepositorio : IMensajeChatRepositorio
    {
        private readonly ApplicationDbContext _context;

        public MensajeChatRepositorio(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task GuardarAsync(MensajeChat mensaje)
        {
            _context.MensajesChat.Add(mensaje);
            await _context.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<MensajeChat>> ObtenerHistorialAsync(int usuarioAId, int usuarioBId)
        {
            var ahora = DateTime.UtcNow;

            return await _context.MensajesChat
                .Include(m => m.Emisor)
                .Where(m =>
                    m.FechaExpiracion > ahora &&
                    (
                        (m.EmisorId == usuarioAId && m.DestinatarioId == usuarioBId) ||
                        (m.EmisorId == usuarioBId && m.DestinatarioId == usuarioAId)
                    ))
                .OrderBy(m => m.FechaEnvio)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task EliminarExpiradosAsync()
        {
            var ahora = DateTime.UtcNow;

            // ExecuteDeleteAsync evita cargar las entidades en memoria:
            // genera un DELETE ... WHERE directamente en la BD.
            await _context.MensajesChat
                .Where(m => m.FechaExpiracion <= ahora)
                .ExecuteDeleteAsync();
        }

        /// <inheritdoc/>
        public async Task<int> EliminarConversacionAsync(int usuarioId, int otroUsuarioId)
        {
            return await _context.MensajesChat
                .Where(m =>
                    (m.EmisorId == usuarioId      && m.DestinatarioId == otroUsuarioId) ||
                    (m.EmisorId == otroUsuarioId  && m.DestinatarioId == usuarioId))
                .ExecuteDeleteAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<MensajeChat>> ObtenerConversacionesActivasAsync(int usuarioId)
        {
            var ahora = DateTime.UtcNow;

            // Traemos todos los mensajes no expirados donde el usuario participó.
            // Para cada par (usuarioId, otroUsuarioId) nos quedamos solo con el
            // mensaje de mayor FechaEnvio, que es el último de esa conversación.
            //
            // La clave de agrupación normaliza el par para que (A→B) y (B→A)
            // caigan en el mismo grupo: usamos Min/Max para que el orden
            // no importe.
            var mensajes = await _context.MensajesChat
                .Include(m => m.Emisor)
                .Include(m => m.Destinatario)
                .Where(m =>
                    m.FechaExpiracion > ahora &&
                    (m.EmisorId == usuarioId || m.DestinatarioId == usuarioId))
                .ToListAsync();

            // Agrupamos en memoria (los resultados ya están filtrados y son pocos
            // para el caso de uso de conversaciones activas de un solo usuario).
            var ultimosPorConversacion = mensajes
                .GroupBy(m => Math.Min(m.EmisorId, m.DestinatarioId) * 1_000_000
                            + Math.Max(m.EmisorId, m.DestinatarioId))
                .Select(g => g.OrderByDescending(m => m.FechaEnvio).First());

            return ultimosPorConversacion;
        }
    }
}
