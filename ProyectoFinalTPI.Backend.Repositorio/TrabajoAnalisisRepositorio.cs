using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public partial class TrabajoAnalisisRepositorio(ApplicationDbContext db) : ITrabajoAnalisisRepositorio
{
    public async Task<ReservaAnalisisDto?> Reservar(DateTime ahora, TimeSpan duracion, int maxIntentos, CancellationToken ct)
    {
        var hasta = ahora.Add(duracion);
        var token = Guid.NewGuid();
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE trabajos_analisis_recuerdos SET "Estado" = 5, "TokenReserva" = NULL,
                    "ReservadoHasta" = NULL, "CodigoError" = 'reintentos_agotados', "FechaActualizacion" = {ahora}
                WHERE "Intentos" >= {maxIntentos} AND
                    ("Estado" = 0 OR ("Estado" = 1 AND "ReservadoHasta" <= {ahora}))
                """, ct);
            var reservas = await db.Database.SqlQuery<ReservaAnalisisDto>($"""
                WITH candidato AS (
                    SELECT "Id" FROM trabajos_analisis_recuerdos
                    WHERE "Intentos" < {maxIntentos} AND
                        (("Estado" = 0 AND "ProximoIntento" <= {ahora}) OR ("Estado" = 1 AND "ReservadoHasta" <= {ahora}))
                    ORDER BY "FechaCreacion", "Id"
                    FOR UPDATE SKIP LOCKED LIMIT 1
                )
                UPDATE trabajos_analisis_recuerdos j
                SET "Estado" = 1, "Intentos" = j."Intentos" + 1, "TokenReserva" = {token},
                    "ReservadoHasta" = {hasta}, "FechaActualizacion" = {ahora}
                FROM candidato c WHERE j."Id" = c."Id"
                RETURNING j."Id", j."PublicacionId", j."VersionContenido", j."Intentos", j."TokenReserva", j."ReservadoHasta"
                """).ToListAsync(ct);
            return reservas.SingleOrDefault();
        }

        // InMemory solo para tests por capa. La reserva concurrente se prueba con PostgreSQL real.
        var agotados = await db.TrabajosAnalisisRecuerdos.Where(j => j.Intentos >= maxIntentos &&
            (j.Estado == EstadoTrabajoAnalisis.Pendiente || (j.Estado == EstadoTrabajoAnalisis.Procesando && j.ReservadoHasta <= ahora))).ToListAsync(ct);
        foreach (var agotado in agotados) Terminar(agotado, EstadoTrabajoAnalisis.Fallido, ahora, "reintentos_agotados");
        await db.SaveChangesAsync(ct);
        var trabajo = await db.TrabajosAnalisisRecuerdos.Where(j => j.Intentos < maxIntentos &&
            ((j.Estado == EstadoTrabajoAnalisis.Pendiente && j.ProximoIntento <= ahora) ||
             (j.Estado == EstadoTrabajoAnalisis.Procesando && j.ReservadoHasta <= ahora)))
            .OrderBy(j => j.FechaCreacion).ThenBy(j => j.Id).FirstOrDefaultAsync(ct);
        if (trabajo == null) return null;
        trabajo.Estado = EstadoTrabajoAnalisis.Procesando; trabajo.Intentos++;
        trabajo.TokenReserva = token; trabajo.ReservadoHasta = hasta; trabajo.FechaActualizacion = ahora;
        await db.SaveChangesAsync(ct);
        return new ReservaAnalisisDto { Id = trabajo.Id, PublicacionId = trabajo.PublicacionId, VersionContenido = trabajo.VersionContenido,
            Intentos = trabajo.Intentos, TokenReserva = token, ReservadoHasta = hasta };
    }

    public async Task<EntradaAnalisisRecuerdoDto?> ObtenerEntrada(ReservaAnalisisDto reserva, DateTime ahora, CancellationToken ct)
    {
        var vigente = await db.TrabajosAnalisisRecuerdos.AsNoTracking().AnyAsync(j => j.Id == reserva.Id &&
            j.Estado == EstadoTrabajoAnalisis.Procesando && j.TokenReserva == reserva.TokenReserva && j.ReservadoHasta > ahora, ct);
        if (!vigente) return null;
        return await db.Publicaciones.AsNoTracking().Where(p => p.Id == reserva.PublicacionId && !p.EstaOculto &&
            p.PublicacionOriginalId == null && p.PermitirMomentosAutomaticos && p.VersionContenido == reserva.VersionContenido)
            .Select(p => new EntradaAnalisisRecuerdoDto(p.Id, p.VersionContenido, p.Titulo, p.Descripcion, p.Fecha)).SingleOrDefaultAsync(ct);
    }

    public Task<bool> Completar(ReservaAnalisisDto reserva, ResultadoAnalisisRecuerdoDto resultado, string versionAnalizador, DateTime ahora, CancellationToken ct) =>
        Transaccion(reserva.PublicacionId, async () =>
        {
            ValidadorPropuestasMomento.Validar(resultado.Propuestas);
            var trabajo = await CargarReserva(reserva, ahora, ct);
            if (trabajo == null) return false;
            var p = await db.Publicaciones.SingleOrDefaultAsync(p => p.Id == reserva.PublicacionId, ct);
            if (p != null) await db.Entry(p).ReloadAsync(ct);
            if (p == null || p.EstaOculto || p.PublicacionOriginalId != null || !p.PermitirMomentosAutomaticos || p.VersionContenido != reserva.VersionContenido)
            {
                Terminar(trabajo, EstadoTrabajoAnalisis.Descartado, ahora);
                await db.SaveChangesAsync(ct);
                return false;
            }
            if (!resultado.AnalisisRealizado)
            {
                trabajo.VersionAnalizador = versionAnalizador;
                Terminar(trabajo, EstadoTrabajoAnalisis.SinAnalizador, ahora);
                await db.SaveChangesAsync(ct);
                return true;
            }
            resultado = resultado with { Momentos = await ResolverPropuestas(resultado, ahora, ct) };
            var ids = resultado.Momentos.Select(m => m.MomentoId).ToArray();
            if (await db.Momentos.CountAsync(m => ids.Contains(m.Id), ct) != ids.Length)
                throw new ArgumentException("El resultado contiene momentos inexistentes.");
            var existentes = await db.PublicacionesMomentos.Where(a => a.PublicacionId == p.Id).ToListAsync(ct);
            foreach (var existente in existentes) await db.Entry(existente).ReloadAsync(ct);
            var cambio = false;
            // Reemplazar solo la selección automática. Las decisiones del autor son inmutables para el analizador.
            foreach (var anterior in existentes.Where(a => a.Origen == OrigenAsociacionMomento.Automatico && !ids.Contains(a.MomentoId)))
            { db.PublicacionesMomentos.Remove(anterior); cambio = true; }
            foreach (var detectado in resultado.Momentos)
            {
                var vinculo = existentes.SingleOrDefault(a => a.MomentoId == detectado.MomentoId);
                if (vinculo?.Origen == OrigenAsociacionMomento.Autor) continue;
                if (vinculo == null)
                {
                    vinculo = new PublicacionMomento { PublicacionId = p.Id, MomentoId = detectado.MomentoId, FechaCreacion = ahora };
                    db.PublicacionesMomentos.Add(vinculo); cambio = true;
                }
                if (vinculo.Estado != EstadoAsociacionMomento.Activo || vinculo.Confianza != detectado.Confianza || vinculo.VersionAnalisis != versionAnalizador)
                    cambio = true;
                vinculo.Estado = EstadoAsociacionMomento.Activo; vinculo.Confianza = detectado.Confianza;
                vinculo.VersionAnalisis = versionAnalizador; vinculo.FechaActualizacion = ahora;
            }
            if (cambio) p.VersionRelaciones = checked(p.VersionRelaciones + 1);
            trabajo.VersionAnalizador = versionAnalizador;
            Terminar(trabajo, resultado.Momentos.Count == 0 ? EstadoTrabajoAnalisis.SinMomento : EstadoTrabajoAnalisis.Completado, ahora);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    public async Task Descartar(ReservaAnalisisDto reserva, DateTime ahora, CancellationToken ct)
    {
        await Transaccion(null, async () =>
        {
            var trabajo = await CargarReserva(reserva, ahora, ct);
            if (trabajo == null) return false;
            Terminar(trabajo, EstadoTrabajoAnalisis.Descartado, ahora);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task RegistrarFallo(ReservaAnalisisDto reserva, string codigo, DateTime ahora, DateTime siguienteIntento, int maxIntentos, CancellationToken ct)
    {
        // Un SaveChanges fallido puede haber dejado cambios en memoria. No confirmarlos junto al reintento.
        db.ChangeTracker.Clear();
        await Transaccion(null, async () =>
        {
            var trabajo = await CargarReserva(reserva, ahora, ct);
            if (trabajo == null) return false;
            Terminar(trabajo, trabajo.Intentos >= maxIntentos ? EstadoTrabajoAnalisis.Fallido : EstadoTrabajoAnalisis.Pendiente, ahora, codigo);
            trabajo.ProximoIntento = siguienteIntento;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task<EstadoAnalisisRecuerdoDto?> ObtenerEstado(int publicacionId, CancellationToken ct)
    {
        var estado = await db.Publicaciones.AsNoTracking().Where(p => p.Id == publicacionId && !p.EstaOculto && p.PublicacionOriginalId == null)
            .Select(p => new { p.Id, p.VersionContenido, p.PermitirMomentosAutomaticos,
                Trabajo = p.TrabajosAnalisis.Where(j => j.VersionContenido == p.VersionContenido).Select(j => (EstadoTrabajoAnalisis?)j.Estado).FirstOrDefault() })
            .SingleOrDefaultAsync(ct);
        if (estado == null) return null;
        return new EstadoAnalisisRecuerdoDto { PublicacionId = estado.Id, VersionContenido = estado.VersionContenido,
            Estado = !estado.PermitirMomentosAutomaticos ? "desactivado" : estado.Trabajo switch
            {
                EstadoTrabajoAnalisis.Pendiente => "pendiente", EstadoTrabajoAnalisis.Procesando => "procesando",
                EstadoTrabajoAnalisis.Completado => "completado", EstadoTrabajoAnalisis.SinMomento => "sinMomento",
                EstadoTrabajoAnalisis.Descartado => "descartado", EstadoTrabajoAnalisis.Fallido => "fallido",
                EstadoTrabajoAnalisis.SinAnalizador => "sinAnalizador", _ => "noProgramado"
            } };
    }

    private async Task<TrabajoAnalisisRecuerdo?> CargarReserva(ReservaAnalisisDto reserva, DateTime ahora, CancellationToken ct)
    {
        TrabajoAnalisisRecuerdo? trabajo;
        if (db.Database.IsNpgsql())
        {
            var filas = await db.TrabajosAnalisisRecuerdos.FromSqlInterpolated(
                $"SELECT * FROM trabajos_analisis_recuerdos WHERE \"Id\" = {reserva.Id} FOR UPDATE").ToListAsync(ct);
            trabajo = filas.SingleOrDefault();
            // Comprobar el vencimiento después de adquirir los bloqueos, no con la hora previa a la espera.
            var horaServidor = await db.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);
            if (horaServidor > ahora) ahora = horaServidor;
        }
        else trabajo = await db.TrabajosAnalisisRecuerdos.SingleOrDefaultAsync(j => j.Id == reserva.Id, ct);
        if (trabajo == null) return null;
        await db.Entry(trabajo).ReloadAsync(ct);
        return trabajo.Estado == EstadoTrabajoAnalisis.Procesando && trabajo.TokenReserva == reserva.TokenReserva &&
            trabajo.ReservadoHasta > ahora ? trabajo : null;
    }

    private async Task<T> Transaccion<T>(int? publicacionId, Func<Task<T>> accion, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return await accion();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (publicacionId != null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(741986, {0})", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(741986, {publicacionId.Value})", ct);
        }
        var resultado = await accion();
        await tx.CommitAsync(ct);
        return resultado;
    }

    private static void Terminar(TrabajoAnalisisRecuerdo trabajo, EstadoTrabajoAnalisis estado, DateTime ahora, string? codigo = null)
    {
        trabajo.Estado = estado; trabajo.TokenReserva = null; trabajo.ReservadoHasta = null;
        trabajo.FechaActualizacion = ahora; trabajo.CodigoError = codigo;
    }
}
