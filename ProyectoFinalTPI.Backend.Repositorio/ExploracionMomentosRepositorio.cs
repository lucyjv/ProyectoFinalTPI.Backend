using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio;

public partial class MomentoRepositorio
{
    public Task<List<MomentoResumenDto>> Buscar(string texto, string textoNormalizado, int? despuesId, int limite, CancellationToken ct) =>
        db.Momentos.AsNoTracking()
            .Where(m => m.MomentoCanonicoId == null && (despuesId == null || m.Id > despuesId) &&
                (texto == "" || m.Nombre.ToLower().Contains(texto) || m.Alias.Any(a => a.TextoNormalizado.Contains(textoNormalizado))))
            .OrderBy(m => m.Id).Take(limite)
            .Select(m => new MomentoResumenDto { Id = m.Id, Nombre = m.Nombre, FechaInicio = m.FechaInicio, FechaFin = m.FechaFin, MomentoPadreId = m.MomentoPadreId })
            .ToListAsync(ct);

    public async Task<MomentoDetalleDto?> ObtenerDetalle(int id, CancellationToken ct)
    {
        if (!await db.Momentos.AnyAsync(m => m.Id == id, ct)) return null;
        var canonico = await CatalogoMomentos.Resolver(db, id, ct);
        var momento = await db.Momentos.AsNoTracking().Include(m => m.MomentoPadre)
            .Include(m => m.Hijos).Include(m => m.Alias).AsSingleQuery().SingleOrDefaultAsync(m => m.Id == canonico.Id, ct);
        return momento == null ? null : new MomentoDetalleDto
        {
            Id = momento.Id, Nombre = momento.Nombre, Descripcion = momento.Descripcion,
            VersionCatalogo = momento.VersionCatalogo, RedirigidoDesdeId = id != momento.Id ? id : null,
            FechaInicio = momento.FechaInicio, FechaFin = momento.FechaFin, MomentoPadreId = momento.MomentoPadreId,
            Padre = momento.MomentoPadre == null ? null : Resumen(momento.MomentoPadre),
            Hijos = momento.Hijos.Where(m => m.MomentoCanonicoId == null).OrderBy(m => m.Nombre).ThenBy(m => m.Id).Select(Resumen).ToList(),
            Alias = momento.Alias.OrderBy(a => a.TextoNormalizado).Select(a => a.Texto).ToList()
        };
    }

    public async Task<List<PublicacionResumenDto>> ObtenerPublicaciones(int id, bool descendientes, CursorExploracionMomentoDto? cursor, int limite, CancellationToken ct)
    {
        id = (await CatalogoMomentos.Resolver(db, id, ct)).Id;
        var ids = new HashSet<int> { id };
        var frontera = new List<int> { id };
        while (descendientes && frontera.Count > 0)
        {
            var hijos = await db.Momentos.AsNoTracking()
                .Where(m => m.MomentoCanonicoId == null && m.MomentoPadreId != null && frontera.Contains(m.MomentoPadreId.Value)).Select(m => m.Id).ToListAsync(ct);
            frontera = hijos.Where(ids.Add).ToList();
        }
        var query = db.Publicaciones.AsNoTracking().Where(p => !p.EstaOculto && p.PublicacionOriginalId == null &&
            p.Momentos.Any(a => ids.Contains(a.MomentoId) && a.Estado == EstadoAsociacionMomento.Activo));
        if (cursor != null)
            query = query.Where(p => p.Fecha < cursor.Fecha || (p.Fecha == cursor.Fecha && p.Id < cursor.Id));
        return await query.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).Take(limite)
            .Select(p => new PublicacionResumenDto
            {
                Id = p.Id, Titulo = p.Titulo, DescripcionBreve = p.Descripcion.Length > 180 ? p.Descripcion.Substring(0, 180) : p.Descripcion,
                Fecha = p.Fecha, Categoria = p.Categoria, UrlMultimedia = p.UrlMultimedia, TipoMultimedia = p.TipoMultimedia,
                Autor = new AutorResumenDto { Id = p.UsuarioId, Username = p.Usuario.Username }
            }).ToListAsync(ct);
    }

    public async Task<MomentosPublicacionDto?> ObtenerEstado(int id, int? usuarioId, CancellationToken ct)
    {
        var publicacion = await db.Publicaciones.AsNoTracking().Include(p => p.Momentos).ThenInclude(a => a.Momento)
            .AsSingleQuery().SingleOrDefaultAsync(p => p.Id == id && !p.EstaOculto && p.PublicacionOriginalId == null, ct);
        return publicacion == null ? null : Estado(publicacion, usuarioId);
    }

    public Task<MomentosPublicacionDto> Corregir(int id, int usuarioId, IReadOnlyCollection<int> momentoIds, bool permitirAutomaticos, long version, CancellationToken ct) =>
        Proteger(id, async () =>
        {
            // Leer una instantánea nueva después del bloqueo, sin reutilizar entidades cacheadas.
            var actual = await db.Publicaciones.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && !p.EstaOculto, ct)
                ?? throw new KeyNotFoundException("La publicación no existe o no está disponible.");
            if (actual.PublicacionOriginalId != null) throw new ArgumentException("Corregí los momentos del recuerdo original.");
            if (actual.UsuarioId != usuarioId) throw new UnauthorizedAccessException("Solo el autor puede corregir su recuerdo.");
            if (actual.VersionRelaciones != version) throw new ConflictoRelacionesException();
            var canonicos = new HashSet<int>();
            foreach (var seleccionado in momentoIds) canonicos.Add((await CatalogoMomentos.Resolver(db, seleccionado, ct)).Id);
            momentoIds = canonicos;
            if (await db.Momentos.CountAsync(m => momentoIds.Contains(m.Id), ct) != momentoIds.Count)
                throw new KeyNotFoundException("Alguno de los momentos seleccionados no existe.");
            var publicacion = await db.Publicaciones.SingleAsync(p => p.Id == id, ct);
            await db.Entry(publicacion).ReloadAsync(ct);
            var vinculos = await db.PublicacionesMomentos.Where(a => a.PublicacionId == id).ToListAsync(ct);
            foreach (var vinculo in vinculos) await db.Entry(vinculo).ReloadAsync(ct);
            var cambio = publicacion.PermitirMomentosAutomaticos != permitirAutomaticos;
            var ahora = DateTime.UtcNow;
            foreach (var vinculo in vinculos)
            {
                var estado = momentoIds.Contains(vinculo.MomentoId) ? EstadoAsociacionMomento.Activo : EstadoAsociacionMomento.Rechazado;
                if (vinculo.Origen == OrigenAsociacionMomento.Autor && vinculo.Estado == estado && vinculo.Confianza == null && vinculo.VersionAnalisis == null) continue;
                vinculo.Origen = OrigenAsociacionMomento.Autor;
                vinculo.Estado = estado;
                vinculo.Confianza = null; vinculo.VersionAnalisis = null; vinculo.FechaActualizacion = ahora;
                cambio = true;
            }
            foreach (var momentoId in momentoIds.Except(vinculos.Select(a => a.MomentoId)))
            {
                db.PublicacionesMomentos.Add(new PublicacionMomento
                {
                    PublicacionId = id, MomentoId = momentoId, Origen = OrigenAsociacionMomento.Autor,
                    FechaCreacion = ahora, FechaActualizacion = ahora
                });
                cambio = true;
            }
            publicacion.PermitirMomentosAutomaticos = permitirAutomaticos;
            if (cambio) publicacion.VersionRelaciones = checked(publicacion.VersionRelaciones + 1);
            await db.SaveChangesAsync(ct);
            return (await ObtenerEstado(id, usuarioId, ct))!;
        }, ct);

    private static MomentoResumenDto Resumen(Momento m) => new()
    {
        Id = m.Id, Nombre = m.Nombre, FechaInicio = m.FechaInicio, FechaFin = m.FechaFin, MomentoPadreId = m.MomentoPadreId
    };

    private static MomentosPublicacionDto Estado(Publicacion p, int? usuarioId) => new()
    {
        PublicacionId = p.Id, VersionRelaciones = p.VersionRelaciones,
        PermitirMomentosAutomaticos = p.PermitirMomentosAutomaticos, PuedeCorregir = p.UsuarioId == usuarioId,
        Momentos = p.Momentos.Where(a => a.Estado == EstadoAsociacionMomento.Activo)
            .OrderBy(a => a.Momento.Nombre).ThenBy(a => a.MomentoId)
            .Select(a => new MomentoAsociadoDto { Momento = Resumen(a.Momento), Origen = a.Origen == OrigenAsociacionMomento.Autor ? "autor" : "automatico" }).ToList()
    };
}
