using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio
{
    public interface ILogrosUsuariosRepositorio
    {
        Task<bool> ExisteRegistro(int usuarioId, int logroId);
        Task Guardar(Logros_Usuarios usuarioLogro);
        Task<List<Logros_Usuarios>> ObtenerPorUsuario(int usuarioId);
    }
}
