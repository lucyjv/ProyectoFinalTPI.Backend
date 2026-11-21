using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public sealed class PuntuacionPublicacionRepositorio(
    ApplicationDbContext contexto, ConsumoDiarioPuntosNostalgiaRepositorio consumo,
    IDriver grafo, TimeProvider reloj, ILogger<PuntuacionPublicacionRepositorio> logger)
{
    private const string PuntuacionesHistoricas = """
        CALL {
            OPTIONAL MATCH (usuario:Usuario)-[r:PUNTUO]->(:Publicacion {postgresId: $publicacionId})
            WHERE usuario.postgresId IS NULL OR NOT (usuario.postgresId IN $usuariosMigrados)
            RETURN coalesce(sum(r.puntaje), 0) AS puntosTotales,
                   max(CASE WHEN usuario.postgresId = $usuarioId THEN r.puntaje END) AS miPuntuacion
        }
        CALL {
            OPTIONAL MATCH (:Usuario {postgresId: $usuarioId})-[r:PUNTUO]->(publicacion:Publicacion)
            WHERE publicacion.postgresId IS NULL OR NOT (publicacion.postgresId IN $publicacionesMigradas)
            RETURN coalesce(sum(r.puntaje), 0) AS puntosOtorgados
        }
        RETURN puntosTotales, miPuntuacion, puntosOtorgados
        """;

    private const string ProyectarPuntuaciones = """
        UNWIND $cambios AS cambio
        FOREACH (_ IN CASE WHEN cambio.puntaje IS NOT NULL THEN [1] ELSE [] END |
            MERGE (usuario:Usuario {postgresId: cambio.usuarioId})
            MERGE (publicacion:Publicacion {postgresId: cambio.publicacionId})
            MERGE (usuario)-[r:PUNTUO]->(publicacion)
            SET r.puntaje = cambio.puntaje, r.fecha = datetime({epochMillis: cambio.fechaUnixMs})
        )
        WITH cambio WHERE cambio.puntaje IS NULL
        MATCH (:Usuario {postgresId: cambio.usuarioId})-[r:PUNTUO]->(:Publicacion {postgresId: cambio.publicacionId})
        DELETE r
        """;

    private const string ResumenHistorico = """
        UNWIND $publicacionesIds AS publicacionId
        WITH publicacionId, coalesce($usuariosMigrados[toString(publicacionId)], []) AS migrados
        OPTIONAL MATCH (usuario:Usuario)-[r:PUNTUO]->(:Publicacion {postgresId: publicacionId})
        WHERE usuario.postgresId IS NULL OR NOT (usuario.postgresId IN migrados)
        RETURN publicacionId, coalesce(sum(r.puntaje), 0) AS puntosTotales,
               max(CASE WHEN usuario.postgresId = $usuarioId THEN r.puntaje END) AS miPuntuacion
        """;

    private DbSet<PuntuacionPublicacionPersistida> Puntuaciones => contexto.Set<PuntuacionPublicacionPersistida>();
    private bool intentoGeneralRealizado;

    public async Task<CambioPuntuacionPublicacion> Actualizar(
        int publicacionId, int usuarioId, int? puntaje, int cupoDiario, CancellationToken ct)
    {
        if (puntaje is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(puntaje));

        var resultado = await ActualizarConCupo(publicacionId, usuarioId, puntaje, cupoDiario, ct);
        await IntentarProyectar(publicacionId, usuarioId, ct);
        return resultado;
    }

    internal async Task<IReadOnlyDictionary<int, (int PuntosTotales, int? MiPuntuacion)>> ObtenerResumen(
        IReadOnlyCollection<int> publicacionesIds, int? usuarioId, CancellationToken ct)
    {
        await ReintentarProyeccion(ct);
        var ids = publicacionesIds.Distinct().ToArray();
        var persistidas = await Puntuaciones.AsNoTracking()
            .Where(puntuacion => ids.Contains(puntuacion.PublicacionId)).ToListAsync(ct);
        return await CombinarResumenConHistorico(ids, usuarioId, persistidas, ct);
    }

    // Las lecturas de puntajes en el grafo reintentan el trabajo que quedó pendiente de pedidos anteriores.
    internal Task ReintentarProyeccion(CancellationToken ct)
    {
        if (intentoGeneralRealizado || !contexto.Database.IsNpgsql())
            return Task.CompletedTask;

        intentoGeneralRealizado = true;
        return IntentarProyectar(null, null, ct);
    }

    private async Task<CambioPuntuacionPublicacion> ActualizarConCupo(
        int publicacionId, int usuarioId, int? puntaje, int cupoDiario, CancellationToken ct)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
        await consumo.BloquearUsuario(usuarioId, ct);
        await BloquearPublicacion(publicacionId, ct);

        var persistidas = await Puntuaciones.AsNoTracking()
            .Where(puntuacion => puntuacion.PublicacionId == publicacionId || puntuacion.UsuarioId == usuarioId)
            .ToListAsync(ct);
        var porPublicacion = persistidas.Where(puntuacion => puntuacion.PublicacionId == publicacionId).ToArray();
        var porUsuario = persistidas.Where(puntuacion => puntuacion.UsuarioId == usuarioId).ToArray();
        var (totalesHistoricos, miHistorica, otorgadosHistoricos) =
            await LeerHistoricas(publicacionId, usuarioId, porPublicacion, porUsuario, ct);
        var persistida = porPublicacion.SingleOrDefault(puntuacion => puntuacion.UsuarioId == usuarioId);
        var anterior = persistida is null ? miHistorica : persistida.Puntaje;

        // Repetir el puntaje vigente conserva el toggle del endpoint.
        var nuevo = puntaje == anterior ? null : puntaje;
        var diferencia = (nuevo ?? 0) - (anterior ?? 0);
        if (nuevo != anterior)
            await GuardarPuntuacionConGasto(publicacionId, usuarioId, nuevo, diferencia, cupoDiario, ct);

        var estado = new PuntuacionPublicacionDto
        {
            PublicacionId = publicacionId,
            MiPuntuacion = nuevo,
            PuntosTotales = checked(totalesHistoricos + porPublicacion.Sum(puntuacion => puntuacion.Puntaje ?? 0) + diferencia)
        };
        var resultado = new CambioPuntuacionPublicacion(
            estado, checked(otorgadosHistoricos + porUsuario.Sum(puntuacion => puntuacion.Puntaje ?? 0) + diferencia));
        await transaccion.CommitAsync(ct);
        return resultado;
    }

    private async Task GuardarPuntuacionConGasto(
        int publicacionId, int usuarioId, int? nuevo, int diferencia, int cupoDiario, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await consumo.GastarConUsuarioBloqueado(usuarioId, ahora, Math.Max(0, diferencia), cupoDiario, ct);

        // Una fila con puntaje nulo impide reimportar una puntuación histórica retirada.
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO puntuaciones_publicacion
                ("UsuarioId", "PublicacionId", "Puntaje", "FechaActualizacion", "PendienteDeProyectar")
            VALUES ({usuarioId}, {publicacionId}, {nuevo}, {ahora}, TRUE)
            ON CONFLICT ("UsuarioId", "PublicacionId") DO UPDATE
            SET "Puntaje" = EXCLUDED."Puntaje", "FechaActualizacion" = EXCLUDED."FechaActualizacion",
                "PendienteDeProyectar" = TRUE
            """, ct);
    }

    private async Task<IReadOnlyDictionary<int, (int PuntosTotales, int? MiPuntuacion)>> CombinarResumenConHistorico(
        int[] publicacionesIds, int? usuarioId, IReadOnlyList<PuntuacionPublicacionPersistida> persistidas, CancellationToken ct)
    {
        var porPublicacion = persistidas.ToLookup(puntuacion => puntuacion.PublicacionId);
        var usuariosMigrados = porPublicacion.ToDictionary(
            grupo => grupo.Key.ToString(CultureInfo.InvariantCulture),
            grupo => (object)grupo.Select(puntuacion => puntuacion.UsuarioId).ToArray());

        await using var sesion = grafo.AsyncSession();
        return await sesion.ExecuteReadAsync<IReadOnlyDictionary<int, (int, int?)>>(async tx =>
        {
            var cursor = await tx.RunAsync(ResumenHistorico, new { publicacionesIds, usuarioId = usuarioId ?? -1, usuariosMigrados });
            var resumen = new Dictionary<int, (int, int?)>();
            await foreach (var fila in cursor)
            {
                ct.ThrowIfCancellationRequested();
                var id = fila["publicacionId"].As<int>();
                resumen[id] = CombinarPuntosDePublicacion(fila, porPublicacion[id], usuarioId);
            }
            return resumen;
        });
    }

    private static (int PuntosTotales, int? MiPuntuacion) CombinarPuntosDePublicacion(
        IRecord historico, IEnumerable<PuntuacionPublicacionPersistida> persistidas, int? usuarioId)
    {
        var propia = persistidas.SingleOrDefault(puntuacion => puntuacion.UsuarioId == usuarioId);
        var miPuntuacion = propia is null ? LeerPuntaje(historico) : propia.Puntaje;
        var total = checked(historico["puntosTotales"].As<int>() + persistidas.Sum(puntuacion => puntuacion.Puntaje ?? 0));
        return (total, miPuntuacion);
    }

    private async Task<(int Totales, int? MiPuntuacion, int Otorgados)> LeerHistoricas(
        int publicacionId, int usuarioId, IReadOnlyList<PuntuacionPublicacionPersistida> porPublicacion,
        IReadOnlyList<PuntuacionPublicacionPersistida> porUsuario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await using var sesion = grafo.AsyncSession();
        return await sesion.ExecuteReadAsync(async transaccion =>
        {
            var cursor = await transaccion.RunAsync(PuntuacionesHistoricas, new
            {
                publicacionId,
                usuarioId,
                usuariosMigrados = porPublicacion.Select(puntuacion => puntuacion.UsuarioId).ToArray(),
                publicacionesMigradas = porUsuario.Select(puntuacion => puntuacion.PublicacionId).ToArray()
            });
            if (!await cursor.FetchAsync())
                return (0, (int?)null, 0);

            return (cursor.Current["puntosTotales"].As<int>(), LeerPuntaje(cursor.Current),
                cursor.Current["puntosOtorgados"].As<int>());
        });
    }

    private static int? LeerPuntaje(IRecord fila) => fila["miPuntuacion"] is { } puntaje ? puntaje.As<int>() : null;

    private async Task BloquearPublicacion(int publicacionId, CancellationToken ct)
    {
        var ids = await contexto.Database.SqlQuery<int>($"""
            SELECT "Id" AS "Value" FROM "Publicaciones"
            WHERE "Id" = {publicacionId} AND NOT "EstaOculto" FOR NO KEY UPDATE
            """).ToListAsync(ct);
        if (ids.Count == 0)
            throw new KeyNotFoundException("La publicación no existe.");
    }

    private async Task IntentarProyectar(int? publicacionId, int? usuarioId, CancellationToken ct)
    {
        try
        {
            await ProyectarLotePendiente(publicacionId, usuarioId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "La proyección de puntuaciones en Neo4j sigue pendiente.");
        }
    }

    private async Task ProyectarLotePendiente(int? publicacionId, int? usuarioId, CancellationToken ct)
    {
        if (!publicacionId.HasValue && !await Puntuaciones.AnyAsync(puntuacion => puntuacion.PendienteDeProyectar, ct))
            return;

        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
        var pendientes = await BloquearLotePendiente(publicacionId, usuarioId, ct);
        if (pendientes.Count == 0)
            return;

        await using var sesion = grafo.AsyncSession();
        await ProyectarEnGrafo(sesion, pendientes);
        await MarcarComoProyectadas(pendientes, ct);
        await transaccion.CommitAsync(ct);
    }

    private Task<List<PuntuacionPublicacionPersistida>> BloquearLotePendiente(
        int? publicacionId, int? usuarioId, CancellationToken ct) =>
        Puntuaciones.FromSqlInterpolated($"""
            SELECT * FROM puntuaciones_publicacion
            WHERE "PendienteDeProyectar"
              AND ({publicacionId ?? 0} = 0 OR "PublicacionId" = {publicacionId ?? 0})
              AND ({usuarioId ?? 0} = 0 OR "UsuarioId" = {usuarioId ?? 0})
            ORDER BY "Id" LIMIT 50 FOR UPDATE SKIP LOCKED
            """).AsNoTracking().ToListAsync(ct);

    private static Task ProyectarEnGrafo(IAsyncSession sesion, IReadOnlyList<PuntuacionPublicacionPersistida> pendientes)
    {
        var cambios = pendientes.Select(puntuacion => new Dictionary<string, object?>
        {
            ["usuarioId"] = puntuacion.UsuarioId,
            ["publicacionId"] = puntuacion.PublicacionId,
            ["puntaje"] = puntuacion.Puntaje,
            ["fechaUnixMs"] = puntuacion.FechaActualizacion.ToUnixTimeMilliseconds()
        }).ToArray();

        return sesion.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(ProyectarPuntuaciones, new { cambios });
            await cursor.ConsumeAsync();
        });
    }

    private Task<int> MarcarComoProyectadas(IReadOnlyList<PuntuacionPublicacionPersistida> puntuaciones, CancellationToken ct)
    {
        var ids = puntuaciones.Select(puntuacion => puntuacion.Id).ToArray();
        return Puntuaciones.Where(puntuacion => ids.Contains(puntuacion.Id))
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(puntuacion => puntuacion.PendienteDeProyectar, false), ct);
    }
}
