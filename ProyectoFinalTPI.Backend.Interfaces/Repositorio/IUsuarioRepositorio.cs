using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
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

        Task<List<AutorResumenDto>> ObtenerUsuariosPorIdsAsync(
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken = default);

        Task<List<int>> ObtenerIdsExistentesAsync(
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken = default);

        Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default);

        Task<bool> ExistePorEmailAsync(string email, CancellationToken cancellationToken = default);

        Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken = default);

        Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken = default);
    }
}
