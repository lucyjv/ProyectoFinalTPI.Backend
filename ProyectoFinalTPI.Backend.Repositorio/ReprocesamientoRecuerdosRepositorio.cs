using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class ReprocesamientoRecuerdosRepositorio(ApplicationDbContext db) : IReprocesamientoRecuerdosRepositorio
{
    public async Task<VistaPreviaReprocesamientoDto> Previsualizar(int limite, int despuesDeId, bool incluirFallidos, CancellationToken ct)
    {
        var filas = await db.Publicaciones.AsNoTracking()
            .Where(p => p.Id > despuesDeId && !p.EstaOculto && p.PublicacionOriginalId == null && p.PermitirMomentosAutomaticos &&
                (!p.TrabajosAnalisis.Any(j => j.VersionContenido == p.VersionContenido) ||
                 p.TrabajosAnalisis.Any(j => j.VersionContenido == p.VersionContenido &&
                    (j.Estado == EstadoTrabajoAnalisis.SinAnalizador || (incluirFallidos && j.Estado == EstadoTrabajoAnalisis.Fallido)))))
            .OrderBy(p => p.Id).Take(limite + 1)
            .Select(p => new { p.Id, p.VersionContenido, p.Titulo,
                Estado = p.TrabajosAnalisis.Where(j => j.VersionContenido == p.VersionContenido).Select(j => (EstadoTrabajoAnalisis?)j.Estado).FirstOrDefault() })
            .ToListAsync(ct);
        var hayMas = filas.Count > limite;
        var seleccion = filas.Take(limite).Select(p => new CandidatoReprocesamientoDto(p.Id, p.VersionContenido, p.Titulo,
            p.Estado == null ? "noProgramado" : p.Estado == EstadoTrabajoAnalisis.SinAnalizador ? "sinAnalizador" : "fallido")).ToArray();
        return new(seleccion, hayMas, hayMas ? seleccion[^1].PublicacionId : null);
    }

    public async Task<ResultadoReprocesamientoDto> Encolar(SolicitudReprocesamientoDto solicitud, DateTime ahora, CancellationToken ct)
    {
        await using var tx = db.Database.IsNpgsql() ? await db.Database.BeginTransactionAsync(ct) : null;
        // Serializar lotes para conservar la separación de horarios y el índice único por versión.
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(741986, {-1})", ct);
            var horaServidor = await db.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);
            if (horaServidor > ahora) ahora = horaServidor;
        }
        var ultimo = await db.TrabajosAnalisisRecuerdos.Where(j => j.Estado == EstadoTrabajoAnalisis.Pendiente)
            .Select(j => (DateTime?)j.ProximoIntento).MaxAsync(ct);
        var siguiente = ultimo.HasValue && ultimo > ahora ? ultimo.Value : ahora;
        var resultados = new List<ItemReprocesamientoDto>();
        foreach (var seleccion in solicitud.Recuerdos.OrderBy(r => r.PublicacionId))
        {
            ct.ThrowIfCancellationRequested();
            Publicacion? publicacion;
            if (db.Database.IsNpgsql())
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(741986, {seleccion.PublicacionId})", ct);
                publicacion = (await db.Publicaciones.FromSqlInterpolated(
                    $"SELECT * FROM \"Publicaciones\" WHERE \"Id\" = {seleccion.PublicacionId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            }
            else publicacion = await db.Publicaciones.SingleOrDefaultAsync(p => p.Id == seleccion.PublicacionId, ct);
            if (publicacion != null) await db.Entry(publicacion).ReloadAsync(ct);
            string? motivo = publicacion == null || publicacion.EstaOculto || publicacion.PublicacionOriginalId != null ? "noDisponible" :
                !publicacion.PermitirMomentosAutomaticos ? "automatizacionDesactivada" :
                publicacion.VersionContenido != seleccion.VersionContenido ? "versionDesactualizada" : null;
            TrabajoAnalisisRecuerdo? trabajo = null;
            if (motivo == null)
            {
                trabajo = await db.TrabajosAnalisisRecuerdos.SingleOrDefaultAsync(j =>
                    j.PublicacionId == seleccion.PublicacionId && j.VersionContenido == seleccion.VersionContenido, ct);
                if (trabajo != null) await db.Entry(trabajo).ReloadAsync(ct);
                motivo = trabajo?.Estado switch
                {
                    EstadoTrabajoAnalisis.Pendiente or EstadoTrabajoAnalisis.Procesando => "yaProgramado",
                    EstadoTrabajoAnalisis.Completado or EstadoTrabajoAnalisis.SinMomento => "yaAnalizado",
                    EstadoTrabajoAnalisis.Descartado => "descartado",
                    EstadoTrabajoAnalisis.Fallido when !solicitud.IncluirFallidos => "fallidoNoIncluido",
                    _ => null
                };
            }
            if (motivo != null)
            {
                resultados.Add(new(seleccion.PublicacionId, seleccion.VersionContenido, "omitido", motivo));
                continue;
            }
            var nuevo = trabajo == null;
            if (nuevo)
            {
                trabajo = new TrabajoAnalisisRecuerdo { PublicacionId = seleccion.PublicacionId,
                    VersionContenido = seleccion.VersionContenido, FechaCreacion = ahora };
                db.TrabajosAnalisisRecuerdos.Add(trabajo);
            }
            siguiente = siguiente.AddSeconds(solicitud.IntervaloSegundos);
            trabajo!.Estado = EstadoTrabajoAnalisis.Pendiente;
            trabajo.Intentos = 0; trabajo.TokenReserva = null; trabajo.ReservadoHasta = null;
            trabajo.CodigoError = null; trabajo.VersionAnalizador = null;
            trabajo.FechaActualizacion = ahora; trabajo.ProximoIntento = siguiente;
            resultados.Add(new(seleccion.PublicacionId, seleccion.VersionContenido, nuevo ? "encolado" : "reprogramado", ProximoIntento: siguiente));
        }
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return new(resultados);
    }
}
