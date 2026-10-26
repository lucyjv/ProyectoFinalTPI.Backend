using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio
{
    public class UsuarioRepositorio : IUsuarioRepositorio
    {
        private readonly ApplicationDbContext _context;

        public UsuarioRepositorio(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Usuario?> ObtenerPorIdAsync(int id)
        {
            return await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<bool> ExisteUsuario(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Usuarios
                .AnyAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<List<AutorResumenDto>> ObtenerUsuariosPorIdsAsync(
            IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(ids);

            if (ids.Count == 0)
                return new List<AutorResumenDto>();

            return await _context.Usuarios
                .AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new AutorResumenDto
                {
                    Id       = u.Id,
                    Username = u.Username
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<int>> ObtenerIdsExistentesAsync(
            IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(ids);

            if (ids.Count == 0)
                return new List<int>();

            return await _context.Usuarios
                .AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }
        public async Task<Usuario_Interactivo?> ObtenerYUsuarioInteractivoPorIdAsync(int id)
        {
            return await _context.Set<Usuario_Interactivo>()
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var emailNormalizado = email.Trim().ToLower();
            return await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == emailNormalizado, cancellationToken);
        }

        public async Task<bool> ExistePorEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var emailNormalizado = email.Trim().ToLower();
            return await _context.Usuarios
                .AnyAsync(u => u.Email.ToLower() == emailNormalizado, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> ExistePorUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            var usernameNormalizado = username.Trim().ToLower();
            return await _context.Usuarios
                .AnyAsync(u => u.Username.ToLower() == usernameNormalizado, cancellationToken);
        }

        public async Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken = default)
        {
            await _context.Usuarios.AddAsync(usuario, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken = default)
        {
            _context.Usuarios.Update(usuario);
            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<Usuario?> ObtenerPorUsernameAsync(
            string username, CancellationToken cancellationToken = default)
        {
            var usernameNormalizado = username.Trim().ToLower();
            return await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Username.ToLower() == usernameNormalizado,
                    cancellationToken);
        }

        public async Task<List<AutorResumenDto>> BuscarPorUsernameAsync(
            string texto, int limite, CancellationToken cancellationToken = default)
        {
            var textoNormalizado = texto.Trim().ToLower();

            return await _context.Usuarios
                .AsNoTracking()
                .Where(usuario => usuario.Username.ToLower().Contains(textoNormalizado))
                .OrderByDescending(usuario => usuario.Username.ToLower().StartsWith(textoNormalizado))
                .ThenBy(usuario => usuario.Username)
                .Take(limite)
                .Select(usuario => new AutorResumenDto
                {
                    Id = usuario.Id,
                    Username = usuario.Username
                })
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<int> ContarPublicacionesVisiblesAsync(
            int usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .AsNoTracking()
                .CountAsync(p => p.UsuarioId == usuarioId && !p.EstaOculto, cancellationToken);
        }
    }
}
