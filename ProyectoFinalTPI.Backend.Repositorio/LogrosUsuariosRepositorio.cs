using System;
using System.Collections.Generic;
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

        public LogrosUsuariosRepositorio(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task <bool> ExisteRegistro(int usuarioId, int logroId)
        {
            return await _context.Logros_Usuarios
                           .AnyAsync(ul => ul.UsuarioId == usuarioId && ul.LogroId == logroId);
        }

        public async Task Guardar(Logros_Usuarios usuarioLogro)
        {
            _context.Logros_Usuarios.Add(usuarioLogro);
            await _context.SaveChangesAsync();
        }

        public async Task <List<Logros_Usuarios>> ObtenerPorUsuario(int usuarioId)
        {
            return await _context.Logros_Usuarios
                           .Include(ul => ul.Logro)
                           .Where(ul => ul.UsuarioId == usuarioId)
                           .OrderByDescending(ul => ul.FechaDesbloqueo)
                           .ToListAsync();
        }
    }
}

