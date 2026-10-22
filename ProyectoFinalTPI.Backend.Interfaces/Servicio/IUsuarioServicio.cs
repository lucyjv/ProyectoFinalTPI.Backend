using ProyectoFinalTPI.Backend.Dtos.Usuario;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    /// <summary>
    /// Contrato de la lógica de negocio de usuarios.
    /// </summary>
    public interface IUsuarioServicio
    {
        /// <summary>
        /// Busca un usuario por su username exacto.
        /// Lanza <see cref="KeyNotFoundException"/> si no existe ningún usuario con ese username.
        /// </summary>
        Task<UsuarioResumenDto> ObtenerPorUsernameAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        /// Busca usuarios por una coincidencia parcial de username.
        /// </summary>
        Task<IReadOnlyList<AutorResumenDto>> BuscarAsync(
            string texto,
            int limite = 10,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtiene el perfil completo de un usuario por su username.
        /// </summary>
        Task<UsuarioPerfilDto> ObtenerPerfilPorUsernameAsync(string username, int? usuarioAutenticadoId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtiene el perfil completo de un usuario por su identificador.
        /// </summary>
        Task<UsuarioPerfilDto> ObtenerPerfilPorIdAsync(int usuarioId, int? usuarioAutenticadoId, CancellationToken cancellationToken = default);
    }
}
