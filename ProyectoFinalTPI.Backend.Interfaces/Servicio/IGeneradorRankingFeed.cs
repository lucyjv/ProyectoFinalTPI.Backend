namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IGeneradorRankingFeed
{
    Task<IReadOnlyList<int>> GenerarAsync(int usuarioId, CancellationToken cancellationToken = default);
}

public interface IProveedorRankingFeed
{
    Task<IReadOnlyList<int>> ObtenerAsync(int usuarioId, CancellationToken cancellationToken = default);
}
