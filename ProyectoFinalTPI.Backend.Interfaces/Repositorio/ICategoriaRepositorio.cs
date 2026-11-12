using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface ICategoriaRepositorio
{
    Task<bool> PromoverHasta(
        int usuarioId,
        ReputacionEnum destino,
        CancellationToken ct = default);
}