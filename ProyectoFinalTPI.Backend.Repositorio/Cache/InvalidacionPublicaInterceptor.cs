using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio.Cache;

public sealed class InvalidacionPublicaInterceptor(CacheLecturasPublicas cache) : SaveChangesInterceptor
{
    private bool pendiente;
    private void Detectar(DbContext? db) => pendiente = db is not null && db.ChangeTracker.Entries()
        .Any(e => (e.Entity is Usuario || e.Entity is Publicacion) && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    { Detectar(eventData.Context); return result; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Detectar(eventData.Context); return ValueTask.FromResult(result); }
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    { if (pendiente) cache.Invalidar(); pendiente = false; return result; }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    { if (pendiente) await cache.InvalidarAsync(CancellationToken.None); pendiente = false; return result; }
}
