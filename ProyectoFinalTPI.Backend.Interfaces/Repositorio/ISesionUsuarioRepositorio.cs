using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface ISesionUsuarioRepositorio
{
    Task AgregarAsync(SesionUsuario sesion, CancellationToken cancellationToken = default);
    Task<SesionUsuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> EstaActivaAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> RotarAsync(SesionUsuario sesion, string refreshTokenHash, DateTime fechaExpiracion, DateTime ultimoUso, CancellationToken cancellationToken = default);
    Task RevocarAsync(SesionUsuario sesion, DateTime fechaRevocacion, CancellationToken cancellationToken = default);
    Task RevocarTodasAsync(int usuarioId, DateTime fechaRevocacion, CancellationToken cancellationToken = default);
}
