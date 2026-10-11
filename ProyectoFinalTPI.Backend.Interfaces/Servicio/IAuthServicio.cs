using ProyectoFinalTPI.Backend.Dtos.Auth;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IAuthServicio
    {
        /// <summary>
        /// Registra un nuevo usuario. Lanza InvalidOperationException si el email ya existe.
        /// </summary>
        Task<AuthRespuestaDto> RegistrarAsync(RegistroDto dto);

        /// <summary>
        /// Autentica un usuario. Lanza UnauthorizedAccessException si las credenciales son incorrectas.
        /// </summary>
        Task<AuthRespuestaDto> LoginAsync(LoginDto dto);
    }
}
