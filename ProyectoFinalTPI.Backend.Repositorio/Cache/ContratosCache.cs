using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
namespace ProyectoFinalTPI.Backend.Repositorio.Cache;
public interface ICacheLecturasPublicas
{
    Task<T?> Obtener<T>(string clave, Func<Task<T?>> consultar, CancellationToken ct) where T : class;
}
public interface IInvalidadorLecturasPublicas
{
    void Invalidar();
    Task InvalidarAsync(CancellationToken ct);
}
public interface IOrigenLecturasPublicas
{
    Task<Usuario?> ObtenerUsuario(int id, CancellationToken ct);
    Task<bool> EsPublicacionVisible(int id, CancellationToken ct);
}
public interface IDetectorCambiosPublicos
{
    bool HayCambios(DbContext? db);
}
public sealed record DatosPerfilPublico(int Id, string Username, string? Foto, string Tipo, string? Nombre, string? Apellido, ReputacionEnum Reputacion, int Puntos);
