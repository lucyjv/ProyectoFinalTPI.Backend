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
    }
}
