using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public interface IPerfilPublicoConsulta
{
    Task<Usuario?> Obtener(int id, CancellationToken cancellationToken = default);
}

public interface IResumenPublicacionConsulta
{
    Task<PublicacionResumenDto?> Obtener(int id, CancellationToken cancellationToken = default);
}
