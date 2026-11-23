using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public sealed class JuegoTesoroRepositorio(ApplicationDbContext contexto) : IJuegoTesoroRepositorio
{
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
