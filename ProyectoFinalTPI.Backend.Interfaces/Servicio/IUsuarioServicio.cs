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
    }
}
