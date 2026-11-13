using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IRecuperacionContrasenaRepositorio
{
    Task<bool> CrearSiCorrespondeAsync(RecuperacionContrasena recuperacion, TimeSpan intervalo,
        CancellationToken cancellationToken = default);
    Task<RecuperacionContrasena?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> RestablecerAsync(Guid id, string tokenHash, string nuevaContrasenaHash,
        CancellationToken cancellationToken = default);
}
