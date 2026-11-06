using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

// reune las candidatas y calcula en que orden mostrarlas
public sealed class GeneradorRankingFeed(
    IPublicacionRepositorio repositorio, IOptions<FeedRecomendacionesOptions> opciones) : IGeneradorRankingFeed
{
    private readonly FeedRecomendacionesOptions configuracion = opciones.Value;

    public async Task<IReadOnlyList<int>> GenerarAsync(int usuarioId, CancellationToken cancellationToken = default)
    {
        var maximo = configuracion.CantidadMaximaCandidatas;
        var ahora = DateTimeOffset.UtcNow;
        var vistasDesde = ahora.AddDays(-configuracion.DiasDeExclusionDeImpresiones).ToUnixTimeMilliseconds();
        IReadOnlyList<InteraccionFeedDto> semillas;
        List<CaracteristicasFeedDto> datosSemillas;
        using (var etapa = new TelemetriaFeed("interacciones"))
        {
            using (var consulta = new TelemetriaFeed("semillas-neo4j"))
            {
                semillas = await repositorio.ObtenerSemillasFeed(usuarioId,
                    ahora.AddDays(-configuracion.DiasDeHistorialParaSemillas).ToUnixTimeMilliseconds(),
                    configuracion.CantidadMaximaSemillas, cancellationToken);
                consulta.Completar();
            }
            using (var consulta = new TelemetriaFeed("semillas-datos"))
            {
                var ids = semillas.Select(s => s.PublicacionId).Distinct().ToArray();
                datosSemillas = ids.Length == 0 ? new() : await repositorio.ObtenerCaracteristicasFeed(ids, cancellationToken);
                consulta.Completar();
            }
            TelemetriaFeed.RegistrarCantidad("semillas", datosSemillas.Count);
            etapa.Completar();
        }

        var candidatas = new Dictionary<int, Candidata>();
        IReadOnlyList<RecomendacionPublicacionDto> colaborativas;
        using (var etapa = new TelemetriaFeed("candidatos"))
        {
            using (var consulta = new TelemetriaFeed("colaborativas"))
            {
                colaborativas = await repositorio.ObtenerRecomendacionesFeed(usuarioId, maximo, vistasDesde,
                    new(configuracion.PesoLike, configuracion.PesoPuntuacion, configuracion.PesoEtiqueta),
                    datosSemillas.Select(s => s.Id).ToArray(),
                    new(configuracion.VecinosPorSemilla, configuracion.PublicacionesPorVecino), cancellationToken);
                var cupo = (int)Math.Ceiling(maximo * configuracion.PorcentajeCandidatasColaborativas / 100d);
                var ids = colaborativas.OrderByDescending(c => c.PuntajeColaborativo)
                    .ThenByDescending(c => c.PublicacionId).Select(c => c.PublicacionId).Distinct().Take(cupo).ToArray();
                var datos = await ObtenerCaracteristicas(ids, cancellationToken);
                await AgregarNuncaVistas(usuarioId, datos, candidatas, maximo, cancellationToken);
                consulta.Completar();
            }

            using (var consulta = new TelemetriaFeed("generales"))
            {
                CursorCronologicoDto? cursor = null;
                var exploradas = 0;
                for (var lote = 0; lote < configuracion.CantidadMaximaLotesGenerales && candidatas.Count < maximo; lote++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var datos = await repositorio.ObtenerCandidatasGeneralesFeed(ahora.UtcDateTime, cursor, maximo, cancellationToken);
                    exploradas += datos.Count;
                    if (datos.Count == 0) break;
                    await AgregarNuncaVistas(usuarioId, datos, candidatas, maximo, cancellationToken);
                    var ultima = datos[^1];
                    cursor = new() { FechaCreacion = ultima.FechaCreacion, Id = ultima.Id };
                    if (datos.Count < maximo) break;
                }
                TelemetriaFeed.RegistrarCantidad("generales-exploradas", exploradas);
                consulta.Completar();
            }

            // Solo buscamos vistas si faltan candidatas. Las recientes son el ualtimo recurso
            foreach (var recientes in new[] { false, true })
            {
                if (candidatas.Count >= maximo) break;
                using var consulta = new TelemetriaFeed(recientes ? "vistas-recientes" : "vistas-antiguas");
                var vistas = await repositorio.ObtenerVistasFeed(usuarioId, vistasDesde, recientes, maximo, cancellationToken);
                var porId = vistas.GroupBy(v => v.PublicacionId).ToDictionary(g => g.Key,
                    g => g.Max(v => v.UltimaImpresionUnixMs));
                var datos = await ObtenerCaracteristicas(porId.Keys.Where(id => !candidatas.ContainsKey(id)).ToArray(), cancellationToken);
                
                // Verificamos exclusiones actuales 
                var interacciones = await ObtenerInteracciones(usuarioId, datos.Select(d => d.Id).ToArray(), cancellationToken);
                foreach (var dato in datos.OrderBy(d => porId[d.Id]).ThenByDescending(d => d.Id))
                {
                    if (candidatas.Count >= maximo) break;
                    if (interacciones.TryGetValue(dato.Id, out var interaccion) &&
                        (interaccion.DioLike || interaccion.MiPuntuacion.HasValue)) continue;
                    candidatas.TryAdd(dato.Id, new(dato, porId[dato.Id]));
                }
                consulta.Completar();
            }
            TelemetriaFeed.RegistrarCantidad("candidatas", candidatas.Count);
            etapa.Completar();
        }

        using var ranking = new TelemetriaFeed("ranking");
        var pesosPorId = semillas.GroupBy(s => s.PublicacionId).ToDictionary(g => g.Key,
            g => g.Max(s => s.DioLike ? 1d : (s.MiPuntuacion ?? 0) / 5d));
        var pesoTotal = datosSemillas.Sum(s => pesosPorId.GetValueOrDefault(s.Id));
        var afinidades = datosSemillas.GroupBy(s => s.Categoria).ToDictionary(g => g.Key,
            g => pesoTotal == 0 ? 0 : g.Sum(s => pesosPorId.GetValueOrDefault(s.Id)) / pesoTotal);
        var idsConLike = semillas.Where(s => s.DioLike).Select(s => s.PublicacionId).ToHashSet();
        var lugares = datosSemillas.Where(s => idsConLike.Contains(s.Id))
            .Select(s => (s.Latitud, s.Longitud)).Distinct().ToArray();
        var puntajes = colaborativas.GroupBy(c => c.PublicacionId).ToDictionary(g => g.Key,
            g => g.Max(c => c.PuntajeColaborativo));
        var resultado = candidatas.Values.Select(c => new
            {
                Candidata = c,
                Grupo = c.UltimaVista is null ? 0 : c.UltimaVista < vistasDesde ? 1 : 2,
                Puntaje = puntajes.GetValueOrDefault(c.Datos.Id)
                    + afinidades.GetValueOrDefault(c.Datos.Categoria) * configuracion.PesoAfinidadCategoria
                    + CalcularCercania(c.Datos, lugares)
            })
            .OrderBy(c => c.Grupo).ThenBy(c => c.Candidata.UltimaVista ?? long.MaxValue)
            .ThenByDescending(c => c.Puntaje).ThenByDescending(c => c.Candidata.Datos.FechaCreacion)
            .ThenByDescending(c => c.Candidata.Datos.Id).Take(maximo).Select(c => c.Candidata.Datos.Id).ToArray();
        ranking.Completar();
        return resultado;
    }

    private async Task AgregarNuncaVistas(int usuarioId, IReadOnlyCollection<CaracteristicasFeedDto> datos,
        Dictionary<int, Candidata> candidatas, int maximo, CancellationToken cancellationToken)
    {
        var nuevas = datos.Where(d => !candidatas.ContainsKey(d.Id)).ToArray();
        var interacciones = await ObtenerInteracciones(usuarioId, nuevas.Select(d => d.Id).ToArray(), cancellationToken);
        foreach (var dato in nuevas)
        {
            if (candidatas.Count >= maximo) break;
            if (interacciones.TryGetValue(dato.Id, out var interaccion) &&
                (interaccion.DioLike || interaccion.MiPuntuacion.HasValue || interaccion.UltimaImpresionUnixMs.HasValue)) continue;
            candidatas.TryAdd(dato.Id, new(dato, null));
        }
    }

    private async Task<List<CaracteristicasFeedDto>> ObtenerCaracteristicas(int[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0) return new();
        using var etapa = new TelemetriaFeed("datos-candidatas");
        var datos = await repositorio.ObtenerCaracteristicasFeed(ids, cancellationToken);
        etapa.Completar();
        return datos;
    }

    private async Task<Dictionary<int, InteraccionFeedDto>> ObtenerInteracciones(int usuarioId, int[] ids,
        CancellationToken cancellationToken)
    {
        if (ids.Length == 0) return new();
        using var etapa = new TelemetriaFeed("interacciones-candidatas");
        var interacciones = await repositorio.ObtenerInteraccionesCandidatasFeed(usuarioId, ids, cancellationToken);
        var resultado = interacciones.GroupBy(i => i.PublicacionId).ToDictionary(g => g.Key, g => new InteraccionFeedDto
        {
            PublicacionId = g.Key, DioLike = g.Any(i => i.DioLike),
            MiPuntuacion = g.Max(i => i.MiPuntuacion), UltimaImpresionUnixMs = g.Max(i => i.UltimaImpresionUnixMs)
        });
        etapa.Completar();
        return resultado;
    }

    private double CalcularCercania(CaracteristicasFeedDto candidata, (double Latitud, double Longitud)[] lugares)
    {
        if (lugares.Length == 0 || configuracion.PesoCercania == 0) return 0;
        var distancia = lugares.Min(l => DistanciaKm(candidata.Latitud, candidata.Longitud, l.Latitud, l.Longitud));
        return configuracion.PesoCercania * Math.Exp(-distancia / configuracion.DistanciaDeReferenciaKm);
    }

    private static double DistanciaKm(double latitud1, double longitud1, double latitud2, double longitud2)
    {
        static double Radianes(double grados) => grados * Math.PI / 180d;
        var dLat = Radianes(latitud2 - latitud1);
        var dLon = Radianes(longitud2 - longitud1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(Radianes(latitud1)) * Math.Cos(Radianes(latitud2))
            * Math.Pow(Math.Sin(dLon / 2), 2);
        a = Math.Clamp(a, 0, 1);
        return 6371d * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private sealed record Candidata(CaracteristicasFeedDto Datos, long? UltimaVista);
}
