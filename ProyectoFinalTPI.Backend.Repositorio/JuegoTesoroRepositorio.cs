using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public sealed class JuegoTesoroRepositorio(ApplicationDbContext contexto) : IJuegoTesoroRepositorio
{
    public Task<RetoTesoro?> ObtenerSemana(DateOnly semana, CancellationToken ct = default) =>
        contexto.RetosTesoro.AsNoTracking().SingleOrDefaultAsync(r => r.SemanaInicio == semana, ct);

    public async Task<IReadOnlyList<UbicacionTesoro>> ListarUbicaciones(CancellationToken ct = default) =>
        await contexto.UbicacionesTesoro.AsNoTracking().OrderBy(u => u.Id).ToListAsync(ct);

    public async Task<UbicacionTesoro> GuardarUbicacion(UbicacionTesoro ubicacion, CancellationToken ct = default)
    {
        if (ubicacion.Id == 0) contexto.UbicacionesTesoro.Add(ubicacion);
        else contexto.UbicacionesTesoro.Update(ubicacion);
        await contexto.SaveChangesAsync(ct);
        return ubicacion;
    }

    public async Task<IReadOnlyList<ProgramacionTesoro>> ListarProgramaciones(DateOnly desde, DateOnly hasta, CancellationToken ct = default) =>
        await contexto.ProgramacionesTesoro.AsNoTracking().Where(p => p.SemanaInicio >= desde && p.SemanaInicio <= hasta).ToListAsync(ct);

    public async Task GuardarProgramacion(ProgramacionTesoro programacion, CancellationToken ct = default) =>
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO programaciones_tesoro ("SemanaInicio", "UbicacionId")
            VALUES ({programacion.SemanaInicio}, {programacion.UbicacionId})
            ON CONFLICT ("SemanaInicio") DO UPDATE SET "UbicacionId" = EXCLUDED."UbicacionId"
            """, ct);

    public async Task EliminarProgramacion(DateOnly semana, CancellationToken ct = default) =>
        await contexto.ProgramacionesTesoro.Where(p => p.SemanaInicio == semana).ExecuteDeleteAsync(ct);

    public async Task ActualizarReto(RetoTesoro reto, CancellationToken ct = default) =>
        await contexto.RetosTesoro.Where(r => r.Id == reto.Id && r.SemanaInicio == reto.SemanaInicio)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.NombreHallazgo, reto.NombreHallazgo)
                .SetProperty(r => r.Pista, reto.Pista).SetProperty(r => r.Latitud, reto.Latitud)
                .SetProperty(r => r.Longitud, reto.Longitud).SetProperty(r => r.ZoomMinimo, reto.ZoomMinimo), ct);

    public async Task<RetoTesoro> ObtenerOCrearSemana(RetoTesoro candidato, CancellationToken ct = default)
    {
        var existente = await contexto.Set<RetoTesoro>().AsNoTracking()
            .SingleOrDefaultAsync(r => r.SemanaInicio == candidato.SemanaInicio, ct);
        if (existente is not null) return existente;

        // Materialización idempotente del catálogo semanal. La restricción única
        // congela una sola pista y ubicación aun ante consultas concurrentes.
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO retos_tesoro ("SemanaInicio", "ClaveUbicacion", "Pista", "NombreHallazgo", "Latitud", "Longitud", "ZoomMinimo")
            VALUES ({candidato.SemanaInicio}, {candidato.ClaveUbicacion}, {candidato.Pista}, {candidato.NombreHallazgo},
                {candidato.Latitud}, {candidato.Longitud}, {candidato.ZoomMinimo})
            ON CONFLICT ("SemanaInicio") DO NOTHING
            """, ct);
        return await contexto.Set<RetoTesoro>().AsNoTracking().SingleAsync(r => r.SemanaInicio == candidato.SemanaInicio, ct);
    }

    public Task<HallazgoTesoro?> ObtenerHallazgo(int retoId, int usuarioId, CancellationToken ct = default) =>
        contexto.Set<HallazgoTesoro>().AsNoTracking().SingleOrDefaultAsync(h => h.RetoId == retoId && h.UsuarioId == usuarioId, ct);

    public async Task<HallazgoTesoro> GuardarHallazgo(HallazgoTesoro hallazgo, CancellationToken ct = default)
    {
        // Una captura por usuario y reto, incluidos doble clic y pestañas simultáneas.
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO hallazgos_tesoro ("RetoId", "UsuarioId", "FechaHallazgoUtc")
            VALUES ({hallazgo.RetoId}, {hallazgo.UsuarioId}, {hallazgo.FechaHallazgoUtc})
            ON CONFLICT ("RetoId", "UsuarioId") DO NOTHING
            """, ct);
        return (await ObtenerHallazgo(hallazgo.RetoId, hallazgo.UsuarioId, ct))!;
    }
}
