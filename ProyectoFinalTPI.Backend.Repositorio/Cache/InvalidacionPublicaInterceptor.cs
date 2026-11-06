using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio.Cache;

public sealed class InvalidacionPublicaInterceptor(IInvalidadorLecturasPublicas cache, IDetectorCambiosPublicos detector) : SaveChangesInterceptor
{
    private bool pendiente;
    private void Detectar(DbContext? db) => pendiente = detector.HayCambios(db);
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    { Detectar(eventData.Context); return result; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Detectar(eventData.Context); return ValueTask.FromResult(result); }
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    { if (pendiente) cache.Invalidar(); pendiente = false; return result; }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    { if (pendiente) await cache.InvalidarAsync(CancellationToken.None); pendiente = false; return result; }
}
