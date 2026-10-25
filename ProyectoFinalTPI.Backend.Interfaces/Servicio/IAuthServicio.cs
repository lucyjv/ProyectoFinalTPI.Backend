using ProyectoFinalTPI.Backend.Dtos.Auth;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IAuthServicio
    {
        /// <summary>
        /// Registra un nuevo usuario.
        /// Lanza <see cref="InvalidOperationException"/> si el email o el username ya están en uso.
        /// </summary>
        Task<ResultadoAutenticacionDto> RegistrarAsync(RegistroDto dto, string? userAgent = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Autentica un usuario. Lanza UnauthorizedAccessException si las credenciales son incorrectas.
        /// </summary>
        Task<ResultadoAutenticacionDto> LoginAsync(LoginDto dto, string? userAgent = null, CancellationToken cancellationToken = default);

        Task<ResultadoAutenticacionDto> RenovarAsync(string refreshToken, CancellationToken cancellationToken = default);

        Task CerrarSesionAsync(string? refreshToken, CancellationToken cancellationToken = default);

        Task CerrarTodasLasSesionesAsync(int usuarioId, CancellationToken cancellationToken = default);
    }
}
