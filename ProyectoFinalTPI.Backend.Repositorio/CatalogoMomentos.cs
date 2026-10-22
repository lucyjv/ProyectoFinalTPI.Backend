using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

internal static class CatalogoMomentos
{
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
        var clave = IdentidadMomento.Clave(nombre, inicio, fin);
        var coincidencias = await db.Momentos.Where(m => m.MomentoCanonicoId == null && m.ClaveIdentidad == clave).ToListAsync(ct);
        // Las filas previas a la migración no reciben una identidad arbitraria ni se fusionan al migrar.
        // Solo este remanente sin clave requiere comparación administrada; las altas nuevas usan el índice único.
        var anteriores = await db.Momentos.Where(m => m.MomentoCanonicoId == null && m.ClaveIdentidad == null).ToListAsync(ct);
        coincidencias.AddRange(anteriores.Where(m => IdentidadMomento.Clave(m.Nombre, m.FechaInicio, m.FechaFin) == clave));
        var alias = NormalizadorMomento.Normalizar(nombre);
        var porAlias = await db.Momentos.Where(m => m.MomentoCanonicoId == null && m.Alias.Any(a => a.TextoNormalizado == alias) &&
            (inicio == null || m.FechaFin == null || m.FechaFin >= inicio) &&
            (fin == null || m.FechaInicio == null || m.FechaInicio <= fin)).ToListAsync(ct);
        coincidencias.AddRange(porAlias.Where(m => coincidencias.All(c => c.Id != m.Id)));
        if (coincidencias.Count > 1) throw new ConflictoCatalogoException("Existen identidades duplicadas; revisar y fusionar los momentos antes de reutilizarlos.");
        return coincidencias.SingleOrDefault();
    }
}
