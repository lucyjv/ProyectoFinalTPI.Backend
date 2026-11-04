using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
namespace ProyectoFinalTPI.Backend.Repositorio.Cache;
public sealed class OrigenLecturasPublicas(ApplicationDbContext db) : IOrigenLecturasPublicas
{
    public Task<Usuario?> ObtenerUsuario(int id, CancellationToken ct) => db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
    public Task<bool> EsPublicacionVisible(int id, CancellationToken ct) => db.Publicaciones.AsNoTracking().AnyAsync(p => p.Id == id && !p.EstaOculto, ct);
}
public sealed class DetectorCambiosPublicos : IDetectorCambiosPublicos
{
    public bool HayCambios(DbContext? db) => db is not null && db.ChangeTracker.Entries()
        .Any(e => (e.Entity is Usuario || e.Entity is Publicacion) && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
}
