using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public sealed class ConsumoDiarioPuntosNostalgiaRepositorio(ApplicationDbContext contexto)
    : IConsumoDiarioPuntosNostalgiaRepositorio
{
    public Task<int> ObtenerPuntosGastados(int usuarioId, DateOnly fecha, CancellationToken ct = default) =>
        contexto.Set<ConsumoDiarioPuntosNostalgia>()
            .Where(consumo => consumo.UsuarioId == usuarioId && consumo.Fecha == fecha)
            .Select(consumo => consumo.PuntosGastados)
            .SingleOrDefaultAsync(ct);

    public Task<int> ObtenerPuntosExtra(int usuarioId, CancellationToken ct = default) =>
        contexto.Set<BilleteraPuntosNostalgia>()
            .Where(b => b.UsuarioId == usuarioId)
            .Select(b => b.PuntosExtra)
            .SingleOrDefaultAsync(ct);

    public async Task AcreditarPuntosExtra(int usuarioId, int puntos, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(puntos);
        var ahora = DateTimeOffset.UtcNow;
        var actual = await ObtenerPuntosExtra(usuarioId, ct);
        var nuevo = actual + puntos;

        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO billeteras_puntos_nostalgia ("UsuarioId", "PuntosExtra", "FechaActualizacion")
            VALUES ({usuarioId}, {nuevo}, {ahora})
            ON CONFLICT ("UsuarioId") DO UPDATE SET "PuntosExtra" = {nuevo}, "FechaActualizacion" = {ahora}
            """, ct);
    }

    // Las operaciones que gastan puntos toman este bloqueo antes de leer o escribir el saldo.
    internal async Task BloquearUsuario(int usuarioId, CancellationToken ct)
    {
        ExigirTransaccion();
        var usuarios = await contexto.Database.SqlQuery<int>($"""
            SELECT "Id" AS "Value" FROM "Usuario"
            WHERE "Id" = {usuarioId} AND NOT "EstaEliminado" FOR NO KEY UPDATE
            """).ToListAsync(ct);
        if (usuarios.Count == 0)
            throw new KeyNotFoundException("El usuario no existe.");
    }

    internal async Task<int> GastarConUsuarioBloqueado(
        int usuarioId, DateTimeOffset instante, int puntos, int cupoDiario, CancellationToken ct)
    {
        ExigirTransaccion();
        ArgumentOutOfRangeException.ThrowIfNegative(puntos);
        ArgumentOutOfRangeException.ThrowIfNegative(cupoDiario);
        var fecha = CalendarioNostalgia.FechaLocal(instante);
        var gastados = await ObtenerPuntosGastados(usuarioId, fecha, ct);
        var restantesDiarios = Math.Max(0, cupoDiario - gastados);
        var puntosExtra = await ObtenerPuntosExtra(usuarioId, ct);
        var totalDisponible = restantesDiarios + puntosExtra;

        if (puntos > totalDisponible)
            throw new CupoDiarioPuntosNostalgiaAgotadoException(totalDisponible);
        if (puntos == 0)
            return totalDisponible;

        if (puntos <= restantesDiarios)
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO consumos_diarios_puntos_nostalgia ("UsuarioId", "Fecha", "PuntosGastados")
                VALUES ({usuarioId}, {fecha}, {gastados + puntos})
                ON CONFLICT ("UsuarioId", "Fecha") DO UPDATE SET "PuntosGastados" = EXCLUDED."PuntosGastados"
                """, ct);
        }
        else
        {
            var excedente = puntos - restantesDiarios;
            await contexto.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO consumos_diarios_puntos_nostalgia ("UsuarioId", "Fecha", "PuntosGastados")
                VALUES ({usuarioId}, {fecha}, {cupoDiario})
                ON CONFLICT ("UsuarioId", "Fecha") DO UPDATE SET "PuntosGastados" = EXCLUDED."PuntosGastados"
                """, ct);

            var nuevoExtra = puntosExtra - excedente;
            await contexto.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO billeteras_puntos_nostalgia ("UsuarioId", "PuntosExtra", "FechaActualizacion")
                VALUES ({usuarioId}, {nuevoExtra}, {instante})
                ON CONFLICT ("UsuarioId") DO UPDATE SET "PuntosExtra" = {nuevoExtra}, "FechaActualizacion" = {instante}
                """, ct);
        }

        return totalDisponible - puntos;
    }

    private void ExigirTransaccion()
    {
        if (contexto.Database.CurrentTransaction is null)
            throw new InvalidOperationException("El gasto de puntos debe formar parte de una transacción.");
    }
}
