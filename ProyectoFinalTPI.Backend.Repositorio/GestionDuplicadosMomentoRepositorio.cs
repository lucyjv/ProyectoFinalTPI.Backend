using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio;

public partial class MomentoRepositorio
{
    public async Task<IReadOnlyList<CandidatoDuplicadoMomentoDto>> BuscarCandidatos(int id, int limite, CancellationToken ct)
    {
        var canonico = await CatalogoMomentos.Resolver(db, id, ct);
        var origen = await db.Momentos.AsNoTracking().Include(m => m.Alias).SingleAsync(m => m.Id == canonico.Id, ct);
        // Comparación del catálogo local: nunca invoca IA ni fusiona a partir de un puntaje.
        // Traer solo metadatos; se filtra por período antes de comparar nombres y alias.
        var candidatos = await db.Momentos.AsNoTracking().Include(m => m.Alias)
            .Where(m => m.MomentoCanonicoId == null && m.Id != origen.Id &&
                (origen.FechaInicio == null || m.FechaFin == null || m.FechaFin >= origen.FechaInicio) &&
                (origen.FechaFin == null || m.FechaInicio == null || m.FechaInicio <= origen.FechaFin)).ToListAsync(ct);
        return candidatos.Where(m => PeriodosCompatibles(origen, m))
            .Select(m => new { Momento = m, Similitud = Similitud(origen, m) }).Where(c => c.Similitud >= 0.5)
            .OrderByDescending(c => c.Similitud).ThenBy(c => c.Momento.Id).Take(limite)
            .Select(c => new CandidatoDuplicadoMomentoDto(Catalogo(c.Momento), Math.Round(c.Similitud, 3),
                c.Similitud == 1 ? "nombreOAliasCoincidente" : "textoSimilar")).ToArray();
    }

    public async Task<VistaFusionMomentoDto> Previsualizar(int origenId, int destinoId, CancellationToken ct)
    {
        var (origen, destino, repetida) = await ValidarFusion(origenId, destinoId, ct);
        if (repetida) return new(Catalogo(origen), Catalogo(destino), 0, 0, 0, true);
        var origenes = await db.PublicacionesMomentos.AsNoTracking().Where(a => a.MomentoId == origen.Id).ToListAsync(ct);
        var destinos = await db.PublicacionesMomentos.AsNoTracking().Where(a => a.MomentoId == destino.Id).ToDictionaryAsync(a => a.PublicacionId, ct);
        var conflictos = origenes.Count(a => destinos.TryGetValue(a.PublicacionId, out var b) && ConflictoAutor(a, b));
        return new(Catalogo(origen), Catalogo(destino), origenes.Count, conflictos,
            await db.Momentos.CountAsync(m => m.MomentoPadreId == origen.Id && m.MomentoCanonicoId == null, ct), false);
    }

    public Task<ResultadoFusionMomentoDto> Fusionar(int origenId, SolicitudFusionMomentoDto solicitud, CancellationToken ct) =>
        Proteger(0, async () =>
        {
            var (vistaOrigen, vistaDestino, repetida) = await ValidarFusion(origenId, solicitud.DestinoId, ct);
            if (repetida) return new ResultadoFusionMomentoDto(origenId, vistaDestino.Id, 0, 0, vistaDestino.VersionCatalogo, true);
            if (vistaOrigen.VersionCatalogo != solicitud.VersionOrigen || vistaDestino.VersionCatalogo != solicitud.VersionDestino)
                throw new ConflictoCatalogoException("El catálogo cambió desde la vista previa. Volver a consultar antes de fusionar.");
            var origen = await db.Momentos.Include(m => m.Alias).SingleAsync(m => m.Id == origenId, ct);
            var destino = await db.Momentos.Include(m => m.Alias).SingleAsync(m => m.Id == solicitud.DestinoId, ct);
            await db.Entry(origen).ReloadAsync(ct); await db.Entry(destino).ReloadAsync(ct);
            // Liberar primero la identidad del origen, dentro de la misma transacción. Esto permite
            // indexar el destino legado aun cuando origen y destino representen la misma clave.
            origen.MomentoCanonicoId = destino.Id;
            await db.SaveChangesAsync(ct);
            var origenes = await db.PublicacionesMomentos.Where(a => a.MomentoId == origen.Id).ToListAsync(ct);
            var destinos = await db.PublicacionesMomentos.Where(a => a.MomentoId == destino.Id).ToDictionaryAsync(a => a.PublicacionId, ct);
            var ahora = DateTime.UtcNow;
            var conflictos = 0;
            foreach (var anterior in origenes.OrderBy(a => a.PublicacionId))
            {
                if (db.Database.IsNpgsql())
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(741986, {anterior.PublicacionId})", ct);
                var p = await db.Publicaciones.SingleAsync(p => p.Id == anterior.PublicacionId, ct);
                await db.Entry(p).ReloadAsync(ct);
                if (destinos.TryGetValue(anterior.PublicacionId, out var actual))
                {
                    if (ConflictoAutor(anterior, actual)) conflictos++;
                    var ganador = ElegirAsociacion(anterior, actual);
                    actual.Origen = ganador.Origen; actual.Estado = ganador.Estado;
                    actual.Confianza = ganador.Confianza; actual.VersionAnalisis = ganador.VersionAnalisis;
                    actual.FechaCreacion = anterior.FechaCreacion < actual.FechaCreacion ? anterior.FechaCreacion : actual.FechaCreacion;
                    actual.FechaActualizacion = ahora;
                }
                else db.PublicacionesMomentos.Add(new PublicacionMomento
                {
                    PublicacionId = anterior.PublicacionId, MomentoId = destino.Id, Origen = anterior.Origen,
                    Estado = anterior.Estado, Confianza = anterior.Confianza, VersionAnalisis = anterior.VersionAnalisis,
                    FechaCreacion = anterior.FechaCreacion, FechaActualizacion = ahora
                });
                db.PublicacionesMomentos.Remove(anterior);
                p.VersionRelaciones = checked(p.VersionRelaciones + 1);
            }
            foreach (var texto in origen.Alias.Select(a => a.Texto).Append(origen.Nombre).Append(destino.Nombre).ToArray())
            {
                var normalizado = NormalizadorMomento.Normalizar(texto);
                if (!destino.Alias.Any(a => a.TextoNormalizado == normalizado))
                    destino.Alias.Add(new MomentoAlias { Texto = texto, TextoNormalizado = normalizado });
            }
            foreach (var hijo in await db.Momentos.Where(m => m.MomentoPadreId == origen.Id && m.MomentoCanonicoId == null).ToListAsync(ct))
                hijo.MomentoPadreId = destino.Id;
            foreach (var redireccion in await db.Momentos.Where(m => m.MomentoCanonicoId == origen.Id).ToListAsync(ct))
                redireccion.MomentoCanonicoId = destino.Id;
            destino.VersionCatalogo++;
            await db.SaveChangesAsync(ct);
            return new ResultadoFusionMomentoDto(origen.Id, destino.Id, origenes.Count, conflictos, destino.VersionCatalogo, false);
        }, ct);

    private async Task<(Momento Origen, Momento Destino, bool Repetida)> ValidarFusion(int origenId, int destinoId, CancellationToken ct)
    {
        var origen = await db.Momentos.AsNoTracking().SingleOrDefaultAsync(m => m.Id == origenId, ct)
            ?? throw new KeyNotFoundException("El momento de origen no existe.");
        var destino = await CatalogoMomentos.Resolver(db, destinoId, ct);
        if (origen.MomentoCanonicoId != null)
        {
            if ((await CatalogoMomentos.Resolver(db, origenId, ct)).Id == destino.Id) return (origen, destino, true);
            throw new ConflictoCatalogoException("El origen ya fue fusionado en otro momento.");
        }
        if (destino.Id != destinoId) throw new ConflictoCatalogoException("El destino cambió de momento canónico. Volver a previsualizar.");
        if (origen.Id == destino.Id) throw new ArgumentException("Elegir dos momentos distintos.");
        if (!PeriodosCompatibles(origen, destino)) throw new ConflictoCatalogoException("Los momentos corresponden a períodos o años distintos.");
        if (await EsAncestro(origen.Id, destino.MomentoPadreId, ct) || await EsAncestro(destino.Id, origen.MomentoPadreId, ct))
            throw new ConflictoCatalogoException("No se puede fusionar un momento con su ancestro o descendiente.");
        return (origen, destino, false);
    }

    private async Task<bool> EsAncestro(int buscado, int? id, CancellationToken ct)
    {
        var visitados = new HashSet<int>();
        while (id.HasValue)
        {
            if (!visitados.Add(id.Value)) throw new ConflictoCatalogoException("La jerarquía contiene un ciclo.");
            if (id == buscado) return true;
            id = (await CatalogoMomentos.Resolver(db, id.Value, ct)).MomentoPadreId;
        }
        return false;
    }

    private static bool PeriodosCompatibles(Momento a, Momento b)
    {
        if ((a.FechaInicio.HasValue && b.FechaFin.HasValue && a.FechaInicio > b.FechaFin) ||
            (b.FechaInicio.HasValue && a.FechaFin.HasValue && b.FechaInicio > a.FechaFin)) return false;
        var aniosA = Regex.Matches(a.Nombre, @"(?<!\d)[12]\d{3}(?!\d)").Select(m => m.Value).ToHashSet();
        var aniosB = Regex.Matches(b.Nombre, @"(?<!\d)[12]\d{3}(?!\d)").Select(m => m.Value).ToHashSet();
        return aniosA.Count == 0 || aniosB.Count == 0 || aniosA.Overlaps(aniosB);
    }

    private static double Similitud(Momento a, Momento b)
    {
        var textosA = a.Alias.Select(x => IdentidadMomento.Normalizar(x.Texto)).Append(IdentidadMomento.Normalizar(a.Nombre)).ToArray();
        var textosB = b.Alias.Select(x => IdentidadMomento.Normalizar(x.Texto)).Append(IdentidadMomento.Normalizar(b.Nombre)).ToArray();
        return textosA.Max(x => textosB.Max(y =>
        {
            var palabrasA = x.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var palabrasB = y.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var union = palabrasA.Union(palabrasB).Count();
            return union == 0 ? 0 : (double)palabrasA.Intersect(palabrasB).Count() / union;
        }));
    }

    private static bool ConflictoAutor(PublicacionMomento a, PublicacionMomento b) =>
        a.Origen == OrigenAsociacionMomento.Autor && b.Origen == OrigenAsociacionMomento.Autor && a.Estado != b.Estado;

    private static PublicacionMomento ElegirAsociacion(PublicacionMomento a, PublicacionMomento b) => new[] { a, b }
        .OrderByDescending(x => x.Origen == OrigenAsociacionMomento.Autor && x.Estado == EstadoAsociacionMomento.Rechazado)
        .ThenByDescending(x => x.Origen == OrigenAsociacionMomento.Autor)
        .ThenByDescending(x => x.Estado == EstadoAsociacionMomento.Activo)
        .ThenByDescending(x => x.Confianza ?? -1).First();

    private static MomentoCatalogoDto Catalogo(Momento m) => new(m.Id, m.Nombre, m.FechaInicio, m.FechaFin, m.VersionCatalogo);
}
