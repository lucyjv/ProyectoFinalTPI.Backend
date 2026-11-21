using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface ICupoPuntosNostalgiaServicio
{
    Task<int> ObtenerCupoParaPuntuar(int usuarioId, PermisoCuenta permiso, CancellationToken ct = default);
    Task<SaldoDiarioPuntosNostalgiaDto> ObtenerSaldo(int usuarioId, CancellationToken ct = default);
}
