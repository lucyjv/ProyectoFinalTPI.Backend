namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IConsumoDiarioPuntosNostalgiaRepositorio
{
    Task<int> ObtenerPuntosGastados(int usuarioId, DateOnly fecha, CancellationToken ct = default);
}
