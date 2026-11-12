using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface ICategoriaRepositorio
{
    Task<bool> PromoverHasta(int usuarioId, ReputacionEnum destino, CancellationToken ct = default);
    Task<Usuario?> ObtenerUsuario(int usuarioId, CancellationToken ct = default);
    Task<bool> TieneRecuerdos(int usuarioId, CancellationToken ct = default);
    Task<int> ObtenerConsumo(int usuarioId, DateOnly semana, CancellationToken ct = default);
    Task<int?> CrearConCupo(Publicacion publicacion, DateOnly semana, int? limite, CancellationToken ct = default);
}