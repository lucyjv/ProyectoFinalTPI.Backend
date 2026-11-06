using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

// reune las candidatas y en que orden mostrarlas
public sealed class GeneradorRankingFeed(
    IPublicacionRepositorio repositorio, IOptions<FeedRecomendacionesOptions> opciones) : IGeneradorRankingFeed
{
    private readonly FeedRecomendacionesOptions configuracion = opciones.Value;

    public async Task<IReadOnlyList<int>> GenerarAsync(int usuarioId, CancellationToken cancellationToken = default)
    {
        var maximo = configuracion.CantidadMaximaCandidatas;
        var ahora = DateTimeOffset.UtcNow;
        var vistasDesde = ahora.AddDays(-configuracion.DiasDeExclusionDeImpresiones).ToUnixTimeMilliseconds();

        var (semillas, datosSemillas) = await ObtenerSemillasConCaracteristicas(usuarioId, ahora, cancellationToken);

        var candidatas = new Dictionary<int, Candidata>();
        IReadOnlyList<RecomendacionPublicacionDto> colaborativas;
        using (var etapa = new TelemetriaFeed("candidatos"))
        {
            colaborativas = await AgregarCandidatasColaborativas(
                usuarioId, datosSemillas, candidatas, maximo, vistasDesde, cancellationToken);

            await CompletarConCandidatasGenerales(usuarioId, ahora, candidatas, maximo, cancellationToken);
            await CompletarConCandidatasVistas(usuarioId, vistasDesde, candidatas, maximo, cancellationToken);

            TelemetriaFeed.RegistrarCantidad("candidatas", candidatas.Count);
            etapa.Completar();
        }

        using var ranking = new TelemetriaFeed("ranking");
        var puntuadas = PuntuarCandidatas(semillas, datosSemillas, colaborativas, candidatas.Values, vistasDesde);

        var resultado = OrdenarCandidatas(puntuadas, maximo);
        ranking.Completar();
        return resultado; // [155, 82, 140, 97]
    }








    private async Task<(IReadOnlyList<InteraccionFeedDto> Semillas, List<CaracteristicasFeedDto> Datos)>
        ObtenerSemillasConCaracteristicas(int usuarioId, DateTimeOffset ahora, CancellationToken cancellationToken)
    // Buscamos las publicaciones que sirven como referencia y sus características
    {
        IReadOnlyList<InteraccionFeedDto> semillas;
        List<CaracteristicasFeedDto> datosSemillas;
        
        using (var etapa = new TelemetriaFeed("interacciones"))
        {
            using (var consulta = new TelemetriaFeed("semillas-neo4j"))
            {
                // Le pide al repositorio las interacciones del usuario en Neo4j
                semillas = await repositorio.ObtenerSemillasFeed(usuarioId,
                    ahora.AddDays(-configuracion.DiasDeHistorialParaSemillas).ToUnixTimeMilliseconds(),
                    configuracion.CantidadMaximaSemillas, cancellationToken);
                consulta.Completar();
            }

            using (var consulta = new TelemetriaFeed("semillas-datos"))
            {
                // Extrae sus IDs, elimina repetidos y busca en PostgreSQL los datos necesarios para compararlas
                // Si no hay semillas, deja una lista vacoia.
                var ids = semillas.Select(s => s.PublicacionId).Distinct().ToArray();
                datosSemillas = ids.Length == 0 ? new() : await repositorio.ObtenerCaracteristicasFeed(ids, cancellationToken);
                consulta.Completar();
            }

            TelemetriaFeed.RegistrarCantidad("semillas", datosSemillas.Count);
            etapa.Completar();
        }
        return (semillas, datosSemillas);
    }

    private async Task<IReadOnlyList<RecomendacionPublicacionDto>> AgregarCandidatasColaborativas(
        int usuarioId, IReadOnlyCollection<CaracteristicasFeedDto> datosSemillas,
        Dictionary<int, Candidata> candidatas, int maximo, long vistasDesde, CancellationToken cancellationToken)
    // buscamos candidatas: publicaciones que podrian aparecer en “Para vos”.
    {
        IReadOnlyList<RecomendacionPublicacionDto> colaborativas;
        using (var consulta = new TelemetriaFeed("colaborativas"))
        {
            // Le pide a Neo4j recomendaciones a partir de las semillas y las relaciones entre usuarios y publicaciones
            //Trae sus IDs con su puntaje
            colaborativas = await repositorio.ObtenerRecomendacionesFeed(
                usuarioId,
                maximo,
                vistasDesde,
                new(
                    configuracion.PesoLike,
                    configuracion.PesoPuntuacion,
                    configuracion.PesoEtiqueta),
                datosSemillas.Select(s => s.Id).ToArray(),

                new(
                    configuracion.VecinosPorSemilla,
                    configuracion.PublicacionesPorVecino),
                cancellationToken);

            // cuantas tomar de este grupo
            // ej:maximo de 500 y un porcentaje de 60%, toma hasta 300.
            var cupo = (int)Math.Ceiling(
                maximo * configuracion.PorcentajeCandidatasColaborativas / 100d);

            //ordena por puntaje, desempata por ID, elimina repetidas y toma hasta ese cupo
            var ids = colaborativas
                .OrderByDescending(c => c.PuntajeColaborativo)
                .ThenByDescending(c => c.PublicacionId)
                .Select(c => c.PublicacionId)
                .Distinct()
                .Take(cupo)
                .ToArray();

            // Busca en PostgreSQL y agrega a candidatas las que el usuario nunca vio, no tiene con like y no puntuo.
            var datos = await ObtenerCaracteristicas(ids, cancellationToken);
            await AgregarNuncaVistas(usuarioId, datos, candidatas, maximo, cancellationToken);
            consulta.Completar();
        }
        return colaborativas;
    }

    private async Task CompletarConCandidatasGenerales(int usuarioId, DateTimeOffset ahora,
        Dictionary<int, Candidata> candidatas, int maximo, CancellationToken cancellationToken)
    // publicaciones generales para completar los lugares que quedaron libres
    {
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

                // Agrega las publicaciones que cumplen con condicione
                await AgregarNuncaVistas(usuarioId, datos, candidatas, maximo, cancellationToken);

                // ultima publicacion recibida
                //fecha e ID marcan desde donde continuar, para avanzar sin volver a recorrer
                var ultima = datos[^1];
                cursor = new() { FechaCreacion = ultima.FechaCreacion, Id = ultima.Id };
                if (datos.Count < maximo) break;
            }
            TelemetriaFeed.RegistrarCantidad("generales-exploradas", exploradas);
            consulta.Completar();
        }
    }

    private async Task CompletarConCandidatasVistas(int usuarioId, long vistasDesde,
        Dictionary<int, Candidata> candidatas, int maximo, CancellationToken cancellationToken)
    //Si aun así faltan candidatas, busca publicaciones que el usuario ya vio
    {
        foreach (var recientes in new[] { false, true })
        {
            if (candidatas.Count >= maximo) break;
            using var consulta = new TelemetriaFeed(recientes ? "vistas-recientes" : "vistas-antiguas");

            var vistas = await repositorio.ObtenerVistasFeed(usuarioId, vistasDesde, recientes, maximo, cancellationToken);

            // Agrupa por publicación y toma la última vista de cada una, para priorizar las más antiguas
            var porId = vistas
                .GroupBy(v => v.PublicacionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Max(v => v.UltimaImpresionUnixMs));

            var datos = await ObtenerCaracteristicas(
                porId.Keys
                    .Where(id => !candidatas.ContainsKey(id))
                    .ToArray(),
                cancellationToken);

            // Prioriza las que hace más tiempo que no ve
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
    }

    private IEnumerable<CandidataPuntuada> PuntuarCandidatas(IReadOnlyList<InteraccionFeedDto> semillas,
        IReadOnlyCollection<CaracteristicasFeedDto> datosSemillas,
        IReadOnlyList<RecomendacionPublicacionDto> colaborativas,
        IEnumerable<Candidata> candidatas, long vistasDesde)
    {
        var afinidades = CalcularAfinidadesPorCategoria(semillas, datosSemillas);
        var lugares = ObtenerLugaresConLike(semillas, datosSemillas);
        var puntajes = colaborativas
            .GroupBy(c => c.PublicacionId)
            .ToDictionary(g => g.Key, g => g.Max(c => c.PuntajeColaborativo));

        return candidatas.Select(c => new CandidataPuntuada(
            Candidata: c,
            Grupo: c.UltimaVista is null ? 0 : c.UltimaVista < vistasDesde ? 1 : 2,
            Puntaje: puntajes.GetValueOrDefault(c.Datos.Id)
                + afinidades.GetValueOrDefault(c.Datos.Categoria) * configuracion.PesoAfinidadCategoria
                + CalcularCercania(c.Datos, lugares)));
    }

    private static Dictionary<CategoriaEnum, double> CalcularAfinidadesPorCategoria(
        IReadOnlyList<InteraccionFeedDto> semillas, IReadOnlyCollection<CaracteristicasFeedDto> datosSemillas)
    {
        // asignamos un peso a cada publicación que usamos como semilla.
        var pesosPorId = semillas
            .GroupBy(s => s.PublicacionId)
            .ToDictionary(
                g => g.Key,
                g => g.Max(s => s.DioLike ? 1d : (s.MiPuntuacion ?? 0) / 5d));

        // cuanto interes representa cada categoria. suma los pesos de las semillas
        var pesoTotal = datosSemillas.Sum(s => pesosPorId.GetValueOrDefault(s.Id));

        // sumamos los pesos por categoria y los divide por el total
        var afinidades = datosSemillas
            .GroupBy(s => s.Categoria)
            .ToDictionary(
                g => g.Key,
                g => pesoTotal == 0
                    ? 0
                    : g.Sum(s => pesosPorId.GetValueOrDefault(s.Id)) / pesoTotal);
        return afinidades;
    }

    private static (double Latitud, double Longitud)[] ObtenerLugaresConLike(
        IReadOnlyList<InteraccionFeedDto> semillas, IReadOnlyCollection<CaracteristicasFeedDto> datosSemillas)
    {
        // traemos los lugares de las publicaciones con like
        var idsConLike = semillas.Where(s => s.DioLike)
            .Select(s => s.PublicacionId)
            .ToHashSet();

        var lugares = datosSemillas.Where(s => idsConLike.Contains(s.Id))
            .Select(s => (s.Latitud, s.Longitud))
            .Distinct()
            .ToArray();
        return lugares;
    }

    private static int[] OrdenarCandidatas(IEnumerable<CandidataPuntuada> candidatas, int maximo)
    {
        return candidatas
            .OrderBy(c => c.Grupo)
            .ThenBy(c => c.Candidata.UltimaVista ?? long.MaxValue)
            .ThenByDescending(c => c.Puntaje)
            .ThenByDescending(c => c.Candidata.Datos.FechaCreacion)
            .ThenByDescending(c => c.Candidata.Datos.Id)
            .Take(maximo)
            .Select(c => c.Candidata.Datos.Id)
            .ToArray();
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
        var resultado = interacciones
            .GroupBy(i => i.PublicacionId)
            .ToDictionary(g => g.Key, g => new InteraccionFeedDto
            {
                PublicacionId = g.Key,
                DioLike = g.Any(i => i.DioLike),
                MiPuntuacion = g.Max(i => i.MiPuntuacion),
                UltimaImpresionUnixMs = g.Max(i => i.UltimaImpresionUnixMs)
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

    private sealed record CandidataPuntuada(Candidata Candidata, int Grupo, double Puntaje);

    private sealed record Candidata(CaracteristicasFeedDto Datos, long? UltimaVista);
}
