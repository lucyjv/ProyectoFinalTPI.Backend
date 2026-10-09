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
    }
}
