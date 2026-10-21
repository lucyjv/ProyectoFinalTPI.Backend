using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio;

public partial class TrabajoAnalisisRepositorio
{
    // Se ejecuta con reserva y contenido vigentes, dentro de la transacción y el bloqueo de catálogo.
    private async Task<IReadOnlyList<MomentoDetectadoDto>> ResolverPropuestas(ResultadoAnalisisRecuerdoDto resultado, DateTime ahora, CancellationToken ct)
    {
        var detectados = new List<MomentoDetectadoDto>();
        foreach (var detectado in resultado.Momentos)
        {
            try { detectados.Add(detectado with { MomentoId = (await CatalogoMomentos.Resolver(db, detectado.MomentoId, ct)).Id }); }
            catch (KeyNotFoundException) { throw new ArgumentException("El resultado contiene momentos inexistentes."); }
        }
        foreach (var propuesta in resultado.Propuestas ?? [])
        {
            Momento momento;
            if (propuesta.MomentoId > 0)
            {
                var canonico = await CatalogoMomentos.Resolver(db, propuesta.MomentoId, ct);
                momento = await db.Momentos.Include(m => m.Alias).SingleAsync(m => m.Id == canonico.Id, ct);
            }
            else
            {
                var nombre = propuesta.Nombre.Trim();
                var normalizado = NormalizadorMomento.Normalizar(nombre);
                var minusculas = nombre.ToLowerInvariant();
                var coincidencias = await db.Momentos.Include(m => m.Alias)
                    .Where(m => m.MomentoCanonicoId == null && (m.Nombre.ToLower() == minusculas || m.Alias.Any(a => a.TextoNormalizado == normalizado))).ToListAsync(ct);
                var identidad = await CatalogoMomentos.BuscarIdentidad(db, nombre, new DateOnly(propuesta.Anio, 1, 1), new DateOnly(propuesta.Anio, 12, 31), ct);
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
        var normalizado = NormalizadorMomento.Normalizar(texto);
        if (!momento.Alias.Any(a => a.TextoNormalizado == normalizado))
            momento.Alias.Add(new MomentoAlias { Texto = texto, TextoNormalizado = normalizado });
    }
}
