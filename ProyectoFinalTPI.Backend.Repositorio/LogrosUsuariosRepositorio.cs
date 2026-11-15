using System;
using System.Collections.Generic;
using ProyectoFinalTPI.Backend.Repositorio.Cache;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio
{
    public class LogrosUsuariosRepositorio : ILogrosUsuariosRepositorio
    {
        private readonly ApplicationDbContext _context;

        private readonly IInvalidadorLecturasPublicas? _cache;

        public LogrosUsuariosRepositorio(ApplicationDbContext context, IInvalidadorLecturasPublicas? cache = null)
        {
            _context = context;
            _cache = cache;
        }
        public async Task <bool> ExisteRegistro(int usuarioId, int logroId)
        {
            return await _context.Logros_Usuarios
                           .AnyAsync(ul => ul.UsuarioId == usuarioId && ul.LogroId == logroId);
        }

        public async Task<bool> Guardar(Logros_Usuarios usuarioLogro)
        {
            if (_context.Database.IsRelational())
            {
                var creado = await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO logros_usuarios ("UsuarioId", "LogroId", "FechaDesbloqueo")
                    VALUES ({usuarioLogro.UsuarioId}, {usuarioLogro.LogroId}, {usuarioLogro.FechaDesbloqueo})
                    ON CONFLICT ("UsuarioId", "LogroId") DO NOTHING
                    """) > 0;
                if (creado && _cache is not null) await _cache.InvalidarAsync(CancellationToken.None);
                return creado;
            }
            if (await ExisteRegistro(usuarioLogro.UsuarioId, usuarioLogro.LogroId)) return false;
            _context.Logros_Usuarios.Add(usuarioLogro);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task <List<Logros_Usuarios>> ObtenerPorUsuario(int usuarioId)
        {
            return await _context.Logros_Usuarios
                           .Include(ul => ul.Logro)
                           .Where(ul => ul.UsuarioId == usuarioId)
                           .OrderByDescending(ul => ul.FechaDesbloqueo)
                           .ToListAsync();
        }

        public Task<List<Logro>> ObtenerCatalogo() =>
            _context.Logro.AsNoTracking().OrderBy(logro => logro.Id).ToListAsync();
    }
}

