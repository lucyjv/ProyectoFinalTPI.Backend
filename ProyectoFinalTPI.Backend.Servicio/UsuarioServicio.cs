using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    /// <summary>
    /// Lógica de negocio de usuarios.
    /// </summary>
    public class UsuarioServicio : IUsuarioServicio
    {
        private readonly IUsuarioRepositorio _repositorio;

        public UsuarioServicio(IUsuarioRepositorio repositorio)
        {
            _repositorio = repositorio;
        }

        /// <inheritdoc/>
        public async Task<UsuarioResumenDto> ObtenerPorUsernameAsync(
            string username, CancellationToken cancellationToken = default)
        {
            var usuario = await _repositorio.ObtenerPorUsernameAsync(username, cancellationToken);

            if (usuario is null)
                throw new KeyNotFoundException($"No existe ningún usuario con el username '{username}'.");

            return new UsuarioResumenDto
            {
                UsuarioId = usuario.Id,
                Username  = usuario.Username
            };
        }
    }
}
