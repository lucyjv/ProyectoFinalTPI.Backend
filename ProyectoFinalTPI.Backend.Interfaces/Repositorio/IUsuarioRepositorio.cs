using ProyectoFinalTPI.Backend.Entidades;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio
{
    public interface IUsuarioRepositorio
    {
        Task<Usuario?> ObtenerPorIdAsync(int id);

        Task<bool> ExisteUsuario(int id, CancellationToken cancellationToken = default);
    }
}
