using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Dtos.Usuario;

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

        /// <summary>
        /// Busca un usuario por su username (coincidencia exacta, sin distinción de mayúsculas).
        /// Devuelve null si no existe.
        /// </summary>
        Task<Usuario?> ObtenerPorUsernameAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        /// Busca usuarios cuyo username contiene el texto indicado, sin distinguir mayúsculas.
        /// </summary>
        Task<List<AutorResumenDto>> BuscarPorUsernameAsync(
            string texto,
            int limite,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cuenta la cantidad de publicaciones visibles de un usuario.
        /// </summary>
        Task<int> ContarPublicacionesVisiblesAsync(int usuarioId, CancellationToken cancellationToken = default);
    }
}
