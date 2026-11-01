using System.Text.RegularExpressions;
using System.Linq.Expressions;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public partial class MomentoRepositorio : IMomentoRepositorio, IExploracionMomentosRepositorio, IGestionDuplicadosMomentoRepositorio, ICatalogoAnalisisRepositorio
{
    private readonly ApplicationDbContext db;
    public MomentoRepositorio(ApplicationDbContext context) => db = context;
    public async Task<Momento?> ObtenerPorId(int id, CancellationToken ct = default)
    {
        if (!await db.Momentos.AnyAsync(m => m.Id == id, ct)) return null;
        var canonico = await MomentoRepositorio.Resolver(db, id, ct);
        return await db.Momentos.AsNoTracking().Include(m => m.Alias).SingleAsync(m => m.Id == canonico.Id, ct);
    }

    public async Task<List<Momento>> ObtenerHijos(int id, CancellationToken ct = default)
    {
        var canonico = await MomentoRepositorio.Resolver(db, id, ct);
        return await db.Momentos.AsNoTracking().Where(m => m.MomentoPadreId == canonico.Id && m.MomentoCanonicoId == null)
            .OrderBy(m => m.Nombre).ThenBy(m => m.Id).ToListAsync(ct);
    }

    public Task<int> Crear(Momento momento, CancellationToken ct = default) => Proteger(0, async () =>
    {
        if (momento.MomentoPadreId.HasValue)
        {
            if (!await db.Momentos.AnyAsync(m => m.Id == momento.MomentoPadreId, ct)) throw new KeyNotFoundException("El momento padre no existe.");
            momento.MomentoPadreId = (await MomentoRepositorio.Resolver(db, momento.MomentoPadreId.Value, ct)).Id;
        }
        await ValidarPadre(momento.Id, momento.MomentoPadreId, ct);
        var existente = await MomentoRepositorio.BuscarIdentidad(db, momento.Nombre, momento.FechaInicio, momento.FechaFin, ct);
        if (existente != null) return existente.Id;
        db.Momentos.Add(momento);
        await db.SaveChangesAsync(ct);
        return momento.Id;
    }, ct);

    public async Task CambiarPadre(int id, int? padreId, CancellationToken ct = default)
    {
        await Proteger(0, async () =>
        {
            id = (await MomentoRepositorio.Resolver(db, id, ct)).Id;
            if (padreId.HasValue) padreId = (await MomentoRepositorio.Resolver(db, padreId.Value, ct)).Id;
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
            alias.MomentoId = (await MomentoRepositorio.Resolver(db, alias.MomentoId, ct)).Id;
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
        momentoId = (await MomentoRepositorio.Resolver(db, momentoId, ct)).Id;
        return await db.PublicacionesMomentos.AsNoTracking().SingleOrDefaultAsync(a => a.PublicacionId == publicacionId && a.MomentoId == momentoId, ct);
    }

    public Task<bool> GuardarAsociacion(PublicacionMomento asociacion, CancellationToken ct = default) =>
        Proteger(asociacion.PublicacionId, async () =>
        {
            asociacion.MomentoId = (await MomentoRepositorio.Resolver(db, asociacion.MomentoId, ct)).Id;
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

    public async Task<IReadOnlyList<CandidatoMomentoDto>> BuscarCandidatos(string texto, CancellationToken ct)
    {
        var palabras = Regex.Matches(ReglasMomento.Normalizar(texto), @"[\p{L}\p{N}]+")
            .Select(m => m.Value).Where(p => p.Length >= 4 || p.All(char.IsDigit))
            .Distinct().OrderByDescending(p => p.All(char.IsDigit)).ThenByDescending(p => p.Length).Take(12).ToArray();
        if (palabras.Length == 0) return [];
        Expression<Func<Momento, bool>> filtro = m => false;
        foreach (var palabra in palabras)
        {
            Expression<Func<Momento, bool>> condicion = m => m.Nombre.ToLower().Contains(palabra) || m.Alias.Any(a => a.TextoNormalizado.Contains(palabra));
            var cuerpo = new ReemplazarParametro(condicion.Parameters[0], filtro.Parameters[0]).Visit(condicion.Body)!;
            filtro = Expression.Lambda<Func<Momento, bool>>(Expression.OrElse(filtro.Body, cuerpo), filtro.Parameters);
        }
        // Búsqueda acotada: no enviar todo el catálogo ni otras publicaciones al proveedor.
        return await db.Momentos.AsNoTracking()
            .Where(m => m.MomentoCanonicoId == null)
            .Where(filtro)
            .OrderBy(m => m.Id).Take(40)
            .Select(m => new CandidatoMomentoDto(m.Id, m.Nombre, m.FechaInicio, m.FechaFin,
                m.Alias.OrderBy(a => a.Id).Take(8).Select(a => a.Texto).ToArray())).ToListAsync(ct);
    }

    private sealed class ReemplazarParametro(ParameterExpression origen, ParameterExpression destino) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == origen ? destino : base.VisitParameter(node);
    }

    internal static async Task<Momento> Resolver(ApplicationDbContext db, int id, CancellationToken ct)
    {
        var visitados = new HashSet<int>();
        while (true)
        {
            if (!visitados.Add(id)) throw new ConflictoCatalogoException("La redirección de momentos contiene un ciclo.");
            var momento = await db.Momentos.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct)
                ?? throw new KeyNotFoundException("El momento no existe.");
            if (momento.MomentoCanonicoId == null) return momento;
            id = momento.MomentoCanonicoId.Value;
        }
    }

    internal static async Task<Momento?> BuscarIdentidad(ApplicationDbContext db, string nombre, DateOnly? inicio, DateOnly? fin, CancellationToken ct)
    {
        var clave = ReglasMomento.ClaveIdentidad(nombre, inicio, fin);
        var coincidencias = await db.Momentos.Where(m => m.MomentoCanonicoId == null && m.ClaveIdentidad == clave).ToListAsync(ct);
        // Las filas previas a la migración no reciben una identidad arbitraria ni se fusionan al migrar.
        // Solo este remanente sin clave requiere comparación administrada; las altas nuevas usan el índice único.
        var anteriores = await db.Momentos.Where(m => m.MomentoCanonicoId == null && m.ClaveIdentidad == null).ToListAsync(ct);
        coincidencias.AddRange(anteriores.Where(m => ReglasMomento.ClaveIdentidad(m.Nombre, m.FechaInicio, m.FechaFin) == clave));
        var alias = ReglasMomento.Normalizar(nombre);
        var porAlias = await db.Momentos.Where(m => m.MomentoCanonicoId == null && m.Alias.Any(a => a.TextoNormalizado == alias) &&
            (inicio == null || m.FechaFin == null || m.FechaFin >= inicio) &&
            (fin == null || m.FechaInicio == null || m.FechaInicio <= fin)).ToListAsync(ct);
        coincidencias.AddRange(porAlias.Where(m => coincidencias.All(c => c.Id != m.Id)));
        if (coincidencias.Count > 1) throw new ConflictoCatalogoException("Existen identidades duplicadas; revisar y fusionar los momentos antes de reutilizarlos.");
        return coincidencias.SingleOrDefault();
    }

    // Se ejecuta con reserva y contenido vigentes, dentro de la transacción y el bloqueo de catálogo.
    internal static async Task<IReadOnlyList<MomentoDetectadoDto>> ResolverPropuestas(ApplicationDbContext db, ResultadoAnalisisRecuerdoDto resultado, DateTime ahora, CancellationToken ct)
    {
        var detectados = new List<MomentoDetectadoDto>();
        foreach (var detectado in resultado.Momentos)
        {
            try { detectados.Add(detectado with { MomentoId = (await MomentoRepositorio.Resolver(db, detectado.MomentoId, ct)).Id }); }
            catch (KeyNotFoundException) { throw new ArgumentException("El resultado contiene momentos inexistentes."); }
        }
        foreach (var propuesta in resultado.Propuestas ?? [])
        {
            Momento momento;
            if (propuesta.MomentoId > 0)
            {
                var canonico = await MomentoRepositorio.Resolver(db, propuesta.MomentoId, ct);
                momento = await db.Momentos.Include(m => m.Alias).SingleAsync(m => m.Id == canonico.Id, ct);
            }
            else
            {
                var nombre = propuesta.Nombre.Trim();
                var normalizado = ReglasMomento.Normalizar(nombre);
                var minusculas = nombre.ToLowerInvariant();
                var coincidencias = await db.Momentos.Include(m => m.Alias)
                    .Where(m => m.MomentoCanonicoId == null && (m.Nombre.ToLower() == minusculas || m.Alias.Any(a => a.TextoNormalizado == normalizado))).ToListAsync(ct);
                var identidad = await MomentoRepositorio.BuscarIdentidad(db, nombre, new DateOnly(propuesta.Anio, 1, 1), new DateOnly(propuesta.Anio, 12, 31), ct);
                if (identidad != null && coincidencias.All(m => m.Id != identidad.Id))
                    coincidencias.Add(await db.Momentos.Include(m => m.Alias).SingleAsync(m => m.Id == identidad.Id, ct));
                coincidencias = coincidencias.Where(m =>
                    (!m.FechaInicio.HasValue || m.FechaInicio.Value.Year <= propuesta.Anio) &&
                    (!m.FechaFin.HasValue || m.FechaFin.Value.Year >= propuesta.Anio)).ToList();
                if (coincidencias.Count > 1) throw new ArgumentException("La identidad del momento es ambigua.");
                momento = coincidencias.SingleOrDefault() ?? new Momento
                {
                    Nombre = nombre, FechaInicio = new DateOnly(propuesta.Anio, 1, 1),
                    FechaFin = new DateOnly(propuesta.Anio, 12, 31), FechaCreacion = ahora
                };
                if (momento.Id == 0) db.Momentos.Add(momento);
                AgregarAlias(momento, nombre);
            }
            foreach (var alias in propuesta.Alias) AgregarAlias(momento, alias.Trim());
            // Obtener el Id sin confirmar la transacción; si falla el vínculo/trabajo también se revierte este alta.
            await db.SaveChangesAsync(ct);
            detectados.Add(new(momento.Id, propuesta.Confianza));
        }
        return detectados.GroupBy(m => m.MomentoId).Select(g => g.MaxBy(m => m.Confianza)!).ToArray();
    }

    private static void AgregarAlias(Momento momento, string texto)
    {
        var normalizado = ReglasMomento.Normalizar(texto);
        if (!momento.Alias.Any(a => a.TextoNormalizado == normalizado))
            momento.Alias.Add(new MomentoAlias { Texto = texto, TextoNormalizado = normalizado });
    }
}
