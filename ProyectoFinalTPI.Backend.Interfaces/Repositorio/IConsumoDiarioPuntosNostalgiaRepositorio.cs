namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IConsumoDiarioPuntosNostalgiaRepositorio
{
    Task<int> ObtenerPuntosGastados(int usuarioId, DateOnly fecha, CancellationToken ct = default);
    Task<int> ObtenerPuntosExtra(int usuarioId, CancellationToken ct = default);
    Task AcreditarPuntosExtra(int usuarioId, int puntos, CancellationToken ct = default);
}
