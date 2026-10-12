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
        public bool ExisteRegistro(int usuarioId, int logroId)
        {
            return _context.Logros_Usuarios
                           .Any(ul => ul.UsuarioId == usuarioId && ul.LogroId == logroId);
        }

        public void Guardar(Logros_Usuarios usuarioLogro)
        {
            _context.Logros_Usuarios.Add(usuarioLogro);
            _context.SaveChanges();
        }

        public List<Logros_Usuarios> ObtenerPorUsuario(int usuarioId)
        {
            return _context.Logros_Usuarios
                           .Include(ul => ul.Logro)
                           .Where(ul => ul.UsuarioId == usuarioId)
                           .OrderByDescending(ul => ul.FechaDesbloqueo)
                           .ToList();
        }
    }
}

