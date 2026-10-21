using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public partial class MomentoRepositorio : IMomentoRepositorio, IExploracionMomentosRepositorio, IGestionDuplicadosMomentoRepositorio
{
    private readonly ApplicationDbContext db;
    public MomentoRepositorio(ApplicationDbContext context) => db = context;
    public async Task<Momento?> ObtenerPorId(int id, CancellationToken ct = default)
    {
        if (!await db.Momentos.AnyAsync(m => m.Id == id, ct)) return null;
        var canonico = await CatalogoMomentos.Resolver(db, id, ct);
        return await db.Momentos.AsNoTracking().Include(m => m.Alias).SingleAsync(m => m.Id == canonico.Id, ct);
    }

    public async Task<List<Momento>> ObtenerHijos(int id, CancellationToken ct = default)
    {
        var canonico = await CatalogoMomentos.Resolver(db, id, ct);
        return await db.Momentos.AsNoTracking().Where(m => m.MomentoPadreId == canonico.Id && m.MomentoCanonicoId == null)
            .OrderBy(m => m.Nombre).ThenBy(m => m.Id).ToListAsync(ct);
    }

    public Task<int> Crear(Momento momento, CancellationToken ct = default) => Proteger(0, async () =>
    {
        if (momento.MomentoPadreId.HasValue)
        {
            if (!await db.Momentos.AnyAsync(m => m.Id == momento.MomentoPadreId, ct)) throw new KeyNotFoundException("El momento padre no existe.");
            momento.MomentoPadreId = (await CatalogoMomentos.Resolver(db, momento.MomentoPadreId.Value, ct)).Id;
        }
        await ValidarPadre(momento.Id, momento.MomentoPadreId, ct);
        var existente = await CatalogoMomentos.BuscarIdentidad(db, momento.Nombre, momento.FechaInicio, momento.FechaFin, ct);
        if (existente != null) return existente.Id;
        db.Momentos.Add(momento);
        await db.SaveChangesAsync(ct);
        return momento.Id;
    }, ct);

    public async Task CambiarPadre(int id, int? padreId, CancellationToken ct = default)
    {
        await Proteger(0, async () =>
        {
            id = (await CatalogoMomentos.Resolver(db, id, ct)).Id;
            if (padreId.HasValue) padreId = (await CatalogoMomentos.Resolver(db, padreId.Value, ct)).Id;
            var momento = await db.Momentos.SingleOrDefaultAsync(m => m.Id == id, ct)
                ?? throw new KeyNotFoundException("El momento no existe.");
            await ValidarPadre(id, padreId, ct);
            momento.MomentoPadreId = padreId;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    private async Task ValidarPadre(int id, int? padreId, CancellationToken ct)
    {
        var visitados = new HashSet<int>();
        if (id > 0) visitados.Add(id);
        while (padreId.HasValue)
        {
            if (!visitados.Add(padreId.Value))
                throw new ArgumentException("La jerarquía de momentos no puede contener ciclos.");
            var padre = await db.Momentos.AsNoTracking().Where(m => m.Id == padreId.Value)
                .Select(m => new { m.MomentoPadreId }).SingleOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("El momento padre no existe.");
            padreId = padre.MomentoPadreId;
        }
    }

    public async Task AgregarAlias(MomentoAlias alias, CancellationToken ct = default)
    {
        await Proteger(0, async () =>
        {
            alias.MomentoId = (await CatalogoMomentos.Resolver(db, alias.MomentoId, ct)).Id;
            if (!await db.Momentos.AnyAsync(m => m.Id == alias.MomentoId, ct))
                throw new KeyNotFoundException("El momento no existe.");
            if (await db.MomentosAlias.AnyAsync(a => a.MomentoId == alias.MomentoId && a.TextoNormalizado == alias.TextoNormalizado, ct))
                return false;
            db.MomentosAlias.Add(alias);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task<PublicacionMomento?> ObtenerAsociacion(int publicacionId, int momentoId, CancellationToken ct = default)
    {
        momentoId = (await CatalogoMomentos.Resolver(db, momentoId, ct)).Id;
        return await db.PublicacionesMomentos.AsNoTracking().SingleOrDefaultAsync(a => a.PublicacionId == publicacionId && a.MomentoId == momentoId, ct);
    }

    public Task<bool> GuardarAsociacion(PublicacionMomento asociacion, CancellationToken ct = default) =>
        Proteger(asociacion.PublicacionId, async () =>
        {
            asociacion.MomentoId = (await CatalogoMomentos.Resolver(db, asociacion.MomentoId, ct)).Id;
            var publicacion = await db.Publicaciones.SingleOrDefaultAsync(p => p.Id == asociacion.PublicacionId, ct)
                ?? throw new KeyNotFoundException("La publicación no existe.");
            await db.Entry(publicacion).ReloadAsync(ct);
            if (asociacion.Origen == OrigenAsociacionMomento.Automatico && !publicacion.PermitirMomentosAutomaticos)
                return false;
            var existente = await db.PublicacionesMomentos.SingleOrDefaultAsync(
                a => a.PublicacionId == asociacion.PublicacionId && a.MomentoId == asociacion.MomentoId, ct);
            if (existente != null)
            {
                // Volver a leer después del bloqueo, incluso si el contexto ya seguía la entidad.
                await db.Entry(existente).ReloadAsync(ct);
                if (existente.Origen == OrigenAsociacionMomento.Autor && asociacion.Origen == OrigenAsociacionMomento.Automatico)
                    return false;
                if (existente.Origen == asociacion.Origen && existente.Estado == asociacion.Estado &&
                    existente.Confianza == asociacion.Confianza && existente.VersionAnalisis == asociacion.VersionAnalisis)
                    return true;
                existente.Origen = asociacion.Origen;
                existente.Estado = asociacion.Estado;
                existente.Confianza = asociacion.Confianza;
                existente.VersionAnalisis = asociacion.VersionAnalisis;
                existente.FechaActualizacion = asociacion.FechaActualizacion;
            }
            else db.PublicacionesMomentos.Add(asociacion);
            publicacion.VersionRelaciones = checked(publicacion.VersionRelaciones + 1);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    public Task<List<Momento>> ObtenerMomentosPublicacion(int publicacionId, CancellationToken ct = default) =>
        db.PublicacionesMomentos.AsNoTracking()
            .Where(a => a.PublicacionId == publicacionId && a.Estado == EstadoAsociacionMomento.Activo &&
                !a.Publicacion.EstaOculto && a.Publicacion.PublicacionOriginalId == null)
            .Select(a => a.Momento).OrderBy(m => m.Nombre).ThenBy(m => m.Id).ToListAsync(ct);

    private async Task<T> Proteger<T>(int recurso, Func<Task<T>> operacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!db.Database.IsNpgsql()) return await operacion(); // Tests de repositorio con InMemory.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        // Catálogo antes de publicación: el mismo orden que análisis y fusiones, sin inversión de bloqueos.
        // El bloqueo ocurre antes de las lecturas y se libera con commit/rollback.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(741986, {0})", ct);
        if (recurso != 0) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(741986, {recurso})", ct);
        var resultado = await operacion();
        await transaccion.CommitAsync(ct);
        return resultado;
    }
}
