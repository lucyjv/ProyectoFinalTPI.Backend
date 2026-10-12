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
        bool ExisteRegistro(int usuarioId, int logroId);
        void Guardar(Logros_Usuarios usuarioLogro);
        List<Logros_Usuarios> ObtenerPorUsuario(int usuarioId);
    }
}
