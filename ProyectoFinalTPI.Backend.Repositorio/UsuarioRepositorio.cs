using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

            return usuario;
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
            {
                return new List<AutorResumenDto>();
            }

            return await _context.Usuarios
                .AsNoTracking()
                .Where(usuario => ids.Contains(usuario.Id))
                .Select(usuario => new AutorResumenDto
                {
                    Id = usuario.Id,
                    Username = usuario.Username
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<int>> ObtenerIdsExistentesAsync(
            IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(ids);

            if (ids.Count == 0)
            {
                return new List<int>();
            }

            return await _context.Usuarios
                .AsNoTracking()
                .Where(usuario => ids.Contains(usuario.Id))
                .Select(usuario => usuario.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }

        public async Task<bool> ExistePorEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Usuarios
                .AnyAsync(u => u.Email == email, cancellationToken);
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
    }
}
