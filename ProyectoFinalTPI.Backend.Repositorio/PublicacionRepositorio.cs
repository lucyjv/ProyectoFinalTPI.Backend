using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Grupo;
using ProyectoFinalTPI.Backend.Dtos.Lugar;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio
{
    public class PublicacionRepositorio : IPublicacionRepositorio
    {
        private const string CrearLikeQuery = """
            MERGE (usuario:Usuario {postgresId: $usuarioId})
            MERGE (publicacion:Publicacion {postgresId: $publicacionId})
            MERGE (usuario)-[like:LE_GUSTA]->(publicacion)
            ON CREATE SET like.creadaEn = datetime()
            """;

        private const string QuitarLikeQuery = """
            MATCH (:Usuario {postgresId: $usuarioId})-[like:LE_GUSTA]->
                  (:Publicacion {postgresId: $publicacionId})
            DELETE like
            """;

        private const string ObtenerEstadoLikeQuery = """
            OPTIONAL MATCH (publicacion:Publicacion {postgresId: $publicacionId})
            OPTIONAL MATCH (usuario:Usuario)-[like:LE_GUSTA]->(publicacion)
            RETURN count(like) AS cantidadLikes,
                   count(CASE WHEN usuario.postgresId = $usuarioId THEN 1 END) > 0 AS dioLike
            """;

        private const string CrearRepostQuery = """
            MERGE (usuario:Usuario {postgresId: $usuarioId})
            MERGE (publicacion:Publicacion {postgresId: $publicacionId})
            MERGE (usuario)-[r:REPOSTEO]->(publicacion)
            ON CREATE SET r.creadaEn = datetime(),
                          r.creadaEnUnixMs = timestamp()
            """;

        private const string QuitarRepostQuery = """
            OPTIONAL MATCH (:Usuario {postgresId: $usuarioId})-[r:REPOSTEO]->
                           (:Publicacion {postgresId: $publicacionId})
            DELETE r
            RETURN count(r) AS cantidad
            """;

        private const string ObtenerEstadoRepostQuery = """
            OPTIONAL MATCH (publicacion:Publicacion {postgresId: $publicacionId})
            OPTIONAL MATCH (usuario:Usuario)-[r:REPOSTEO]->(publicacion)
            RETURN count(r) AS cantidadReposts,
                   count(CASE WHEN usuario.postgresId = $usuarioId THEN 1 END) > 0 AS dioRepost
            """;


        private const string PuntuarPublicacionQuery = """
            MERGE (usuario:Usuario {postgresId: $usuarioId})
            MERGE (publicacion:Publicacion {postgresId: $publicacionId})
            MERGE (usuario)-[puntuacion:PUNTUO]->(publicacion)
            SET puntuacion.puntaje = $puntaje,
                puntuacion.fecha = datetime()
            """;

        private const string QuitarPuntuacionQuery = """
            MATCH (:Usuario {postgresId: $usuarioId})-[puntuacion:PUNTUO]->
                  (:Publicacion {postgresId: $publicacionId})
            DELETE puntuacion
            """;

        private const string ObtenerEstadoPuntuacionQuery = """
            OPTIONAL MATCH (publicacion:Publicacion {postgresId: $publicacionId})
            OPTIONAL MATCH (u:Usuario)-[r:PUNTUO]->(publicacion)
            WITH publicacion,
                 coalesce(sum(r.puntaje), 0) AS puntosTotales
            OPTIONAL MATCH (:Usuario {postgresId: $usuarioId})-[miRating:PUNTUO]->(publicacion)
            RETURN puntosTotales,
                   miRating.puntaje AS miPuntuacion
            """;

        private const string ObtenerResumenPuntuacionesQuery = """
            UNWIND $publicacionesIds AS publicacionId
            OPTIONAL MATCH (publicacion:Publicacion {postgresId: publicacionId})
            OPTIONAL MATCH (u:Usuario)-[r:PUNTUO]->(publicacion)
            WITH publicacionId,
                 coalesce(sum(r.puntaje), 0) AS puntosTotales
            OPTIONAL MATCH (:Usuario {postgresId: $usuarioId})-[miRating:PUNTUO]->(:Publicacion {postgresId: publicacionId})
            RETURN publicacionId,
                   puntosTotales,
                   miRating.puntaje AS miPuntuacion
            """;
        private const string SumarPuntosOtorgadosQuery = """
            MATCH (:Usuario {postgresId: $usuarioId})-[r:PUNTUO]->(:Publicacion)
            RETURN coalesce(sum(r.puntaje), 0) AS totalPuntos
            """;

        // Cada semilla aporta una sola vez, aunque el usuario le haya dado like y la haya puntuado.
        // Los pesos de likes, puntuaciones altas y etiquetas llegan como parámetros configurables.
        private const string RecomendacionesFeedQuery = """
            MATCH (usuario:Usuario {postgresId: $usuarioId})
            OPTIONAL MATCH (usuario)-[:LE_GUSTA]->(semillaLike:Publicacion)
            OPTIONAL MATCH (usuario)-[miPunt:PUNTUO]->(semillaPunt:Publicacion)
            WHERE miPunt.puntaje >= 3
            WITH usuario, collect(DISTINCT semillaLike) + collect(DISTINCT semillaPunt) AS semillas
            UNWIND semillas AS semilla
            WITH DISTINCT usuario, semilla
            CALL {
                WITH semilla
                MATCH (otroUsuario:Usuario)-[:LE_GUSTA]->(semilla)
                MATCH (otroUsuario)-[:LE_GUSTA]->(candidata:Publicacion)
                RETURN candidata, count(DISTINCT otroUsuario) * $pesoLike AS relevancia
                UNION ALL
                WITH semilla
                MATCH (otroUsuario:Usuario)-[p1:PUNTUO]->(semilla)
                MATCH (otroUsuario)-[p2:PUNTUO]->(candidata:Publicacion)
                WHERE p1.puntaje >= 3 AND p2.puntaje >= 3
                RETURN candidata, count(DISTINCT otroUsuario) * $pesoPuntuacion AS relevancia
                UNION ALL
                WITH semilla
                MATCH (etiquetado:Usuario)-[:ETIQUETADO_EN]->(semilla)
                MATCH (etiquetado)-[:ETIQUETADO_EN]->(candidata:Publicacion)
                RETURN candidata, count(DISTINCT etiquetado) * $pesoEtiqueta AS relevancia
            }
            WITH usuario, candidata, relevancia
            WHERE candidata.postgresId IS NOT NULL
              AND NOT (usuario)-[:LE_GUSTA]->(candidata)
              AND NOT (usuario)-[:PUNTUO]->(candidata)
              AND NOT EXISTS {
                  MATCH (usuario)-[vista:VIO]->(candidata)
                  WHERE vista.ultimaVez >= $vistasDesdeUnixMs
              }
            RETURN candidata.postgresId AS publicacionId, sum(relevancia) AS puntaje
            ORDER BY puntaje DESC, publicacionId DESC
            LIMIT $cantidadMaxima
            """;

        private const string ObtenerInteraccionesFeedQuery = """
            MATCH (usuario:Usuario {postgresId: $usuarioId})
            CALL {
                WITH usuario
                MATCH (usuario)-[:LE_GUSTA]->(publicacion:Publicacion)
                RETURN publicacion.postgresId AS publicacionId, true AS dioLike,
                       null AS miPuntuacion, null AS ultimaImpresionUnixMs
                UNION
                WITH usuario
                MATCH (usuario)-[puntuacion:PUNTUO]->(publicacion:Publicacion)
                RETURN publicacion.postgresId AS publicacionId, false AS dioLike,
                       puntuacion.puntaje AS miPuntuacion, null AS ultimaImpresionUnixMs
                UNION
                WITH usuario
                MATCH (usuario)-[vista:VIO]->(publicacion:Publicacion)
                RETURN publicacion.postgresId AS publicacionId, false AS dioLike,
                       null AS miPuntuacion, vista.ultimaVez AS ultimaImpresionUnixMs
            }
            WITH publicacionId, dioLike, miPuntuacion, ultimaImpresionUnixMs
            WHERE publicacionId IS NOT NULL
            RETURN publicacionId, dioLike, miPuntuacion, ultimaImpresionUnixMs
            """;

        private const string RegistrarImpresionesFeedQuery = """
            MERGE (usuario:Usuario {postgresId: $usuarioId})
            WITH usuario
            UNWIND $publicacionesIds AS publicacionId
            MERGE (publicacion:Publicacion {postgresId: publicacionId})
            MERGE (usuario)-[vista:VIO]->(publicacion)
            SET vista.ultimaVez = $ultimaVezUnixMs
            """;

        private readonly ApplicationDbContext _context;
        private readonly IDriver? _neo4jDriver;

        public PublicacionRepositorio(ApplicationDbContext context, IDriver? neo4jDriver = null)
        {
            _context = context;
            _neo4jDriver = neo4jDriver;
        }

        public async Task CrearLike( int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            await session.ExecuteWriteAsync(async transaction =>
            {
                await transaction.RunAsync(CrearLikeQuery, new { publicacionId, usuarioId });
            });
        }

        public async Task QuitarLike( int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            await session.ExecuteWriteAsync(async transaction =>
            {
                await transaction.RunAsync(QuitarLikeQuery, new { publicacionId, usuarioId });
            });
        }

        public async Task<MeGustaPublicacionDto> ObtenerEstadoLike( int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync( ObtenerEstadoLikeQuery, new { publicacionId, usuarioId });
                var registro = await cursor.SingleAsync(); // recibe los valores de cantidadLikes y dioLike para pasarlos al DTO

                return new MeGustaPublicacionDto
                {
                    PublicacionId = publicacionId,
                    CantidadLikes = registro["cantidadLikes"].As<int>(),
                    DioLike = registro["dioLike"].As<bool>()
                };
            });
        }

        public async Task<IReadOnlyDictionary<int, (int Cantidad, bool DioLike)>> ObtenerResumenLikes(
            IReadOnlyCollection<int> publicacionesIds, int usuarioId, CancellationToken cancellationToken = default)
        {
            if (publicacionesIds.Count == 0)
                return new Dictionary<int, (int Cantidad, bool DioLike)>();

            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync("""
                    UNWIND $publicacionesIds AS publicacionId
                    OPTIONAL MATCH (usuario:Usuario)-[like:LE_GUSTA]->
                                  (:Publicacion {postgresId: publicacionId})
                    RETURN publicacionId,
                           count(like) AS cantidadLikes,
                           count(CASE WHEN usuario.postgresId = $usuarioId THEN 1 END) > 0 AS dioLike
                    """, new { publicacionesIds = publicacionesIds.Distinct().ToArray(), usuarioId });

                var resumenes = new Dictionary<int, (int Cantidad, bool DioLike)>();
                await foreach (var registro in cursor)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    resumenes[registro["publicacionId"].As<int>()] =
                        (registro["cantidadLikes"].As<int>(), registro["dioLike"].As<bool>());
                }
                return (IReadOnlyDictionary<int, (int Cantidad, bool DioLike)>)resumenes;
            });
        }

        public async Task CrearRepost(int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            await session.ExecuteWriteAsync(async transaction =>
            {
                await transaction.RunAsync(CrearRepostQuery, new { publicacionId, usuarioId });
            });
        }

        public async Task<bool> QuitarRepost(int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteWriteAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(QuitarRepostQuery, new { publicacionId, usuarioId });
                var registro = await cursor.SingleAsync();
                return registro["cantidad"].As<int>() > 0;
            });
        }

        public async Task<RepostPublicacionDto> ObtenerEstadoRepost(int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(ObtenerEstadoRepostQuery, new { publicacionId, usuarioId });
                var registro = await cursor.SingleAsync();

                return new RepostPublicacionDto
                {
                    PublicacionId = publicacionId,
                    CantidadReposts = registro["cantidadReposts"].As<int>(),
                    DioRepost = registro["dioRepost"].As<bool>()
                };
            });
        }

        public async Task<IReadOnlyDictionary<int, (int Cantidad, bool DioRepost)>> ObtenerResumenReposts(
            IReadOnlyCollection<int> publicacionesIds, int usuarioId, CancellationToken cancellationToken = default)
        {
            if (publicacionesIds.Count == 0)
                return new Dictionary<int, (int Cantidad, bool DioRepost)>();

            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync("""
                    UNWIND $publicacionesIds AS publicacionId
                    OPTIONAL MATCH (usuario:Usuario)-[r:REPOSTEO]->
                                  (:Publicacion {postgresId: publicacionId})
                    RETURN publicacionId,
                           count(r) AS cantidadReposts,
                           count(CASE WHEN usuario.postgresId = $usuarioId THEN 1 END) > 0 AS dioRepost
                    """, new { publicacionesIds = publicacionesIds.Distinct().ToArray(), usuarioId });

                var resumenes = new Dictionary<int, (int Cantidad, bool DioRepost)>();
                await foreach (var registro in cursor)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    resumenes[registro["publicacionId"].As<int>()] =
                        (registro["cantidadReposts"].As<int>(), registro["dioRepost"].As<bool>());
                }
                return (IReadOnlyDictionary<int, (int Cantidad, bool DioRepost)>)resumenes;
            });
        }

        public async Task<IReadOnlyList<(int PublicacionId, int ReposteadorId, string ReposteadorUsername, DateTime Fecha)>> ObtenerRepostsDeSeguidos(
            IReadOnlyCollection<int> autoresIds, CancellationToken cancellationToken = default)
        {
            if (autoresIds.Count == 0)
                return Array.Empty<(int, int, string, DateTime)>();

            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync("""
                    MATCH (reposteador:Usuario)-[r:REPOSTEO]->(publicacion:Publicacion)
                    WHERE reposteador.postgresId IN $autoresIds
                    RETURN publicacion.postgresId AS publicacionId,
                           reposteador.postgresId AS reposteadorId,
                           coalesce(reposteador.username, '') AS reposteadorUsername,
                           coalesce(r.creadaEnUnixMs, 0) AS fechaUnixMs
                    """, new { autoresIds = autoresIds.ToArray() });

                var lista = new List<(int PublicacionId, int ReposteadorId, string ReposteadorUsername, DateTime Fecha)>();
                await foreach (var reg in cursor)
                {
                    var pubId = reg["publicacionId"].As<int>();
                    var repId = reg["reposteadorId"].As<int>();
                    var username = reg["reposteadorUsername"].As<string>();
                    var unixMs = reg["fechaUnixMs"].As<long>();

                    DateTime fecha = unixMs > 0
                        ? DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime
                        : DateTime.UtcNow;

                    lista.Add((pubId, repId, username, fecha));
                }
                return (IReadOnlyList<(int PublicacionId, int ReposteadorId, string ReposteadorUsername, DateTime Fecha)>)lista;
            });
        }

        public async Task PuntuarPublicacion(int publicacionId, int usuarioId, int puntaje, CancellationToken cancellationToken = default)

        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            await session.ExecuteWriteAsync(async transaction =>
            {
                await transaction.RunAsync(PuntuarPublicacionQuery, new { publicacionId, usuarioId, puntaje });
            });
        }

        public async Task QuitarPuntuacion(int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            await session.ExecuteWriteAsync(async transaction =>
            {
                await transaction.RunAsync(QuitarPuntuacionQuery, new { publicacionId, usuarioId });
            });
        }

        public async Task<PuntuacionPublicacionDto> ObtenerEstadoPuntuacion(int publicacionId, int? usuarioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(ObtenerEstadoPuntuacionQuery, new { publicacionId, usuarioId = usuarioId ?? -1 });
                if (!await cursor.FetchAsync())
                {
                    return new PuntuacionPublicacionDto
                    {
                        PublicacionId = publicacionId,
                        PuntosTotales = 0,
                        MiPuntuacion = null
                    };
                }

                var registro = cursor.Current;
                int? miPuntaje = null;
                if (registro["miPuntuacion"] != null)
                {
                    try { miPuntaje = registro["miPuntuacion"].As<int?>(); } catch { }
                }

                return new PuntuacionPublicacionDto
                {
                    PublicacionId = publicacionId,
                    PuntosTotales = registro["puntosTotales"].As<int?>() ?? 0,
                    MiPuntuacion = miPuntaje
                };
            });
        }

        public async Task<IReadOnlyDictionary<int, (int PuntosTotales, int? MiPuntuacion)>> ObtenerResumenPuntuaciones(
            IReadOnlyCollection<int> publicacionesIds, int? usuarioId, CancellationToken cancellationToken = default)
        {
            if (publicacionesIds.Count == 0)
                return new Dictionary<int, (int PuntosTotales, int? MiPuntuacion)>();

            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    ObtenerResumenPuntuacionesQuery,
                    new { publicacionesIds = publicacionesIds.Distinct().ToArray(), usuarioId = usuarioId ?? -1 });

                var resumenes = new Dictionary<int, (int PuntosTotales, int? MiPuntuacion)>();
                await foreach (var registro in cursor)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int? miPuntaje = null;
                    if (registro["miPuntuacion"] != null)
                    {
                        try { miPuntaje = registro["miPuntuacion"].As<int?>(); } catch { }
                    }

                    resumenes[registro["publicacionId"].As<int>()] =
                        (registro["puntosTotales"].As<int?>() ?? 0,
                         miPuntaje);
                }
                return (IReadOnlyDictionary<int, (int PuntosTotales, int? MiPuntuacion)>)resumenes;
            });
        }

        public async Task<IReadOnlyList<RecomendacionPublicacionDto>> ObtenerRecomendacionesFeed(
            int usuarioId, int cantidadMaxima, long vistasDesdeUnixMs, PesosRecomendacionFeedDto pesos,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(RecomendacionesFeedQuery,
                    new
                    {
                        usuarioId, cantidadMaxima, vistasDesdeUnixMs,
                        pesoLike = pesos.Like,
                        pesoPuntuacion = pesos.Puntuacion,
                        pesoEtiqueta = pesos.Etiqueta
                    });
                var recomendaciones = new List<RecomendacionPublicacionDto>();
                await foreach (var registro in cursor)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    recomendaciones.Add(new RecomendacionPublicacionDto
                    {
                        PublicacionId = registro["publicacionId"].As<int>(),
                        PuntajeColaborativo = registro["puntaje"].As<long>()
                    });
                }
                return (IReadOnlyList<RecomendacionPublicacionDto>)recomendaciones;
            });
        }

        public async Task<IReadOnlyList<InteraccionFeedDto>> ObtenerInteraccionesFeed(
            int usuarioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(ObtenerInteraccionesFeedQuery, new { usuarioId });
                var interacciones = new List<InteraccionFeedDto>();
                await foreach (var registro in cursor)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var puntuacion = registro["miPuntuacion"];
                    var ultimaImpresion = registro["ultimaImpresionUnixMs"];
                    interacciones.Add(new InteraccionFeedDto
                    {
                        PublicacionId = registro["publicacionId"].As<int>(),
                        DioLike = registro["dioLike"].As<bool>(),
                        MiPuntuacion = puntuacion is null ? null : puntuacion.As<int>(),
                        UltimaImpresionUnixMs = ultimaImpresion is null ? null : ultimaImpresion.As<long>()
                    });
                }
                return (IReadOnlyList<InteraccionFeedDto>)interacciones;
            });
        }

        public async Task RegistrarImpresionesFeed(int usuarioId, IReadOnlyCollection<int> publicacionesIds,
            CancellationToken cancellationToken = default)
        {
            if (publicacionesIds.Count == 0) return;
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            await session.ExecuteWriteAsync(async transaction =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await transaction.RunAsync(RegistrarImpresionesFeedQuery, new
                {
                    usuarioId,
                    publicacionesIds = publicacionesIds.Distinct().ToArray(),
                    ultimaVezUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            });
        }

        private IDriver ObtenerNeo4jDriver() => _neo4jDriver ??
            throw new InvalidOperationException("El driver de Neo4j no esta configurado");

        public List<Publicacion> ObtenerTodasPublicaciones()
        {
            return _context.Publicaciones
                           .Where(p => !p.EstaOculto)
                           .Include(p => p.Usuario)
                           .Include(p => p.Lugar)
                           .ToList();
        }

        public void GuardarPublicacion(Publicacion publicacion)
        {
            _context.Publicaciones.Add(publicacion);
            _context.SaveChanges();
        }

        public List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria)
        {
            return _context.Publicaciones
                           .Where(p => p.Categoria == categoria && !p.EstaOculto)
                           .ToList();
        }

        public async Task<int> Crear(Publicacion publicacion)
        {
            await _context.Publicaciones.AddAsync(publicacion);
            await _context.SaveChangesAsync();
            return publicacion.Id;
        }

        public async Task<Publicacion?> ObtenerPorId(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .Include(p => p.Lugar)
                .FirstOrDefaultAsync(p => p.Id == id && !p.EstaOculto, cancellationToken);
        }

        public async Task<List<PublicacionPinDto>> ObtenerPines(PublicacionFiltroDto filtro, int limite, CancellationToken cancellationToken = default)
        {
            var consulta = _context.Publicaciones
                .AsNoTracking()
                .Where(p => !p.EstaOculto && p.PublicacionOriginalId == null)
                .Where(p =>
                p.Lugar.Coordenadas.Y >= filtro.MinLatitud &&
                p.Lugar.Coordenadas.Y <= filtro.MaxLatitud &&
                p.Lugar.Coordenadas.X >= filtro.MinLongitud &&
                p.Lugar.Coordenadas.X <= filtro.MaxLongitud);

            if (filtro.Categoria.HasValue)
            {
                consulta = consulta.Where(
                    p => p.Categoria == filtro.Categoria.Value);
            }

            if (filtro.FechaDesde.HasValue)
            {
                consulta = consulta.Where(
                    p => p.Fecha >= filtro.FechaDesde.Value);
            }

            if (filtro.FechaHasta.HasValue)
            {
                consulta = consulta.Where(
                    p => p.Fecha < filtro.FechaHasta.Value);
            }

            return await consulta
                .OrderBy(p => p.Id)
                .Select(p => new PublicacionPinDto
                {
                    Id = p.Id,
                    Latitud = p.Lugar.Coordenadas.Y,
                    Longitud = p.Lugar.Coordenadas.X,
                    Categoria = p.Categoria,
                    Titulo = p.Titulo,
                    Año = p.Fecha.Year
                })
                .Take(limite + 1)
                .ToListAsync(cancellationToken);
        }

        public async Task<PublicacionResumenDto?> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default)
        {
            const int longitudMaximaDescripcion = 180;

            return await _context.Publicaciones
                .AsNoTracking()
                .Where(p => p.Id == id && !p.EstaOculto)
                .Select(p => new PublicacionResumenDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,

                    DescripcionBreve = p.Descripcion.Length > longitudMaximaDescripcion
                    ? p.Descripcion.Substring(
                        0, longitudMaximaDescripcion) + "..."
                        : p.Descripcion,

                    Fecha = p.Fecha,
                    Categoria = p.Categoria,
                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,

                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    }
                })
                .SingleOrDefaultAsync(cancellationToken);

        }

        public async Task<PublicacionDetalleDto?> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .AsNoTracking()
                .Where(p => p.Id == id && !p.EstaOculto)
                .Select(p => new PublicacionDetalleDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,
                    Descripcion = p.Descripcion,

                    Fecha = p.Fecha,
                    FechaCreacion = p.FechaCreación,
                    Categoria = p.Categoria,

                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,

                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    },

                    Lugar = new LugarDetalleDto
                    {
                        Id = p.LugarId,
                        Nombre = p.Lugar.Nombre,
                        Direccion = p.Lugar.Direccion,
                        Latitud = p.Lugar.Coordenadas.Y,
                        Longitud = p.Lugar.Coordenadas.X
                    },
                    CantidadLikes = 0,
                    CantidadComentarios = p.Comentarios.Count(c => !c.EstaOculto && !c.EstaEliminado),
                    DioLike = false,
                    Grupo = p.GrupoId.HasValue ? new GrupoResumenDto
                    {
                        Id = p.Grupo!.Id,
                        Nombre = p.Grupo.Nombre,
                        Descripcion = p.Grupo.Descripcion,
                        Decada = p.Grupo.Decada,
                        FechaCreacion = p.Grupo.FechaCreacion,
                        Creador = new AutorResumenDto
                        {
                            Id = p.Grupo.CreadorId,
                            Username = p.Grupo.Creador.Username
                        },
                        CantidadIntegrantes = p.Grupo.Integrantes.Count
                    } : null,
                    PublicacionOriginal = p.PublicacionOriginal != null ? new PublicacionResumenDto
                    {
                        Id = p.PublicacionOriginal.Id,
                        Titulo = p.PublicacionOriginal.Titulo,
                        DescripcionBreve = p.PublicacionOriginal.Descripcion.Length > 180
                            ? p.PublicacionOriginal.Descripcion.Substring(0, 180) + "..."
                            : p.PublicacionOriginal.Descripcion,
                        Fecha = p.PublicacionOriginal.Fecha,
                        Categoria = p.PublicacionOriginal.Categoria,
                        UrlMultimedia = p.PublicacionOriginal.UrlMultimedia,
                        TipoMultimedia = p.PublicacionOriginal.TipoMultimedia,
                        Autor = new AutorResumenDto
                        {
                            Id = p.PublicacionOriginal.UsuarioId,
                            Username = p.PublicacionOriginal.Usuario.Username
                        }
                    } : null
                })
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<List<PublicacionDetalleDto>> ObtenerFeedSeguidos(IReadOnlyCollection<int> autoresIds, CursorCronologicoDto? cursor, int limite,
        CancellationToken cancellationToken = default)
        {
            if (autoresIds.Count == 0)
            {
                return new List<PublicacionDetalleDto>();
            }

            const int longitudMaximaDescripcion = 300;

            var consulta = _context.Publicaciones
                .AsNoTracking()
                .Where(p =>
                    !p.EstaOculto &&
                    autoresIds.Contains(p.UsuarioId));

            if (cursor != null)
            {
                consulta = consulta.Where(p =>
                    p.FechaCreación < cursor.FechaCreacion ||
                    (p.FechaCreación == cursor.FechaCreacion &&
                     p.Id < cursor.Id));
            }

            return await consulta
                .OrderByDescending(p => p.FechaCreación)
                .ThenByDescending(p => p.Id)
                .Select(p => new PublicacionDetalleDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,

                    Descripcion =
                        p.Descripcion.Length > longitudMaximaDescripcion
                            ? p.Descripcion.Substring(
                                0, longitudMaximaDescripcion - 3) + "..."
                            : p.Descripcion,

                    Fecha = p.Fecha,
                    FechaCreacion = p.FechaCreación,
                    Categoria = p.Categoria,

                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,

                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    },

                    Lugar = new LugarDetalleDto
                    {
                        Id = p.LugarId,
                        Nombre = p.Lugar.Nombre,
                        Direccion = p.Lugar.Direccion,
                        Latitud = p.Lugar.Coordenadas.Y,
                        Longitud = p.Lugar.Coordenadas.X
                    },
                    CantidadLikes = 0,
                    CantidadComentarios = p.Comentarios.Count(c => !c.EstaOculto && !c.EstaEliminado),
                    DioLike = false,
                    Grupo = p.GrupoId.HasValue ? new GrupoResumenDto
                    {
                        Id = p.Grupo!.Id,
                        Nombre = p.Grupo.Nombre,
                        Descripcion = p.Grupo.Descripcion,
                        Decada = p.Grupo.Decada,
                        FechaCreacion = p.Grupo.FechaCreacion,
                        Creador = new AutorResumenDto
                        {
                            Id = p.Grupo.CreadorId,
                            Username = p.Grupo.Creador.Username
                        },
                        CantidadIntegrantes = p.Grupo.Integrantes.Count
                    } : null,
                    PublicacionOriginal = p.PublicacionOriginal != null ? new PublicacionResumenDto
                    {
                        Id = p.PublicacionOriginal.Id,
                        Titulo = p.PublicacionOriginal.Titulo,
                        DescripcionBreve = p.PublicacionOriginal.Descripcion.Length > 180
                            ? p.PublicacionOriginal.Descripcion.Substring(0, 180) + "..."
                            : p.PublicacionOriginal.Descripcion,
                        Fecha = p.PublicacionOriginal.Fecha,
                        Categoria = p.PublicacionOriginal.Categoria,
                        UrlMultimedia = p.PublicacionOriginal.UrlMultimedia,
                        TipoMultimedia = p.PublicacionOriginal.TipoMultimedia,
                        Autor = new AutorResumenDto
                        {
                            Id = p.PublicacionOriginal.UsuarioId,
                            Username = p.PublicacionOriginal.Usuario.Username
                        }
                    } : null
                })
                .Take(limite + 1)
                .ToListAsync(cancellationToken);
        }

        public Task<bool> ExistePublicacionVisible(int id, CancellationToken cancellationToken = default)
        {
            return _context.Publicaciones
                .AnyAsync(
                    p => p.Id == id && !p.EstaOculto,
                    cancellationToken);
        }

        public Task<List<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(
            IReadOnlyCollection<CategoriaEnum> categorias, DateTime fechaCorte,
            CursorCronologicoDto? cursor, int limite, CancellationToken cancellationToken = default)
        {
            return ObtenerPublicacionesParaVos(categorias, fechaCorte, cursor, limite,
                Array.Empty<int>(), cancellationToken);
        }

        public async Task<List<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(
            IReadOnlyCollection<CategoriaEnum> categorias, DateTime fechaCorte,
            CursorCronologicoDto? cursor, int limite, IReadOnlyCollection<int> excluirIds,
            CancellationToken cancellationToken = default)
        {
            var consulta = _context.Publicaciones
                .AsNoTracking()
                .Where(p =>
                    !p.EstaOculto &&
                    p.FechaCreación <= fechaCorte);

            if (excluirIds.Count > 0)
            {
                consulta = consulta.Where(p => !excluirIds.Contains(p.Id));
            }

            if (categorias.Count > 0)
            {
                consulta = consulta.Where(
                    p => categorias.Contains(p.Categoria));
            }

            if (cursor != null)
            {
                consulta = consulta.Where(p =>
                    p.FechaCreación < cursor.FechaCreacion ||
                    (p.FechaCreación == cursor.FechaCreacion &&
                     p.Id < cursor.Id));
            }

            return await consulta
                .OrderByDescending(p => p.FechaCreación)
                .ThenByDescending(p => p.Id)
                .Select(p => new PublicacionDetalleDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,

                    Descripcion = p.Descripcion.Length > 300
                        ? p.Descripcion.Substring(0, 297) + "..."
                        : p.Descripcion,

                    Fecha = p.Fecha,
                    FechaCreacion = p.FechaCreación,
                    Categoria = p.Categoria,

                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,

                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    },

                    Lugar = new LugarDetalleDto
                    {
                        Id = p.LugarId,
                        Nombre = p.Lugar.Nombre,
                        Direccion = p.Lugar.Direccion,
                        Latitud = p.Lugar.Coordenadas.Y,
                        Longitud = p.Lugar.Coordenadas.X
                    },
                    CantidadLikes = 0,
                    CantidadComentarios = p.Comentarios.Count(c => !c.EstaOculto && !c.EstaEliminado),
                    DioLike = false,
                    Grupo = p.GrupoId.HasValue ? new GrupoResumenDto
                    {
                        Id = p.Grupo!.Id,
                        Nombre = p.Grupo.Nombre,
                        Descripcion = p.Grupo.Descripcion,
                        Decada = p.Grupo.Decada,
                        FechaCreacion = p.Grupo.FechaCreacion,
                        Creador = new AutorResumenDto
                        {
                            Id = p.Grupo.CreadorId,
                            Username = p.Grupo.Creador.Username
                        },
                        CantidadIntegrantes = p.Grupo.Integrantes.Count
                    } : null,
                    PublicacionOriginal = p.PublicacionOriginal != null ? new PublicacionResumenDto
                    {
                        Id = p.PublicacionOriginal.Id,
                        Titulo = p.PublicacionOriginal.Titulo,
                        DescripcionBreve = p.PublicacionOriginal.Descripcion.Length > 180
                            ? p.PublicacionOriginal.Descripcion.Substring(0, 180) + "..."
                            : p.PublicacionOriginal.Descripcion,
                        Fecha = p.PublicacionOriginal.Fecha,
                        Categoria = p.PublicacionOriginal.Categoria,
                        UrlMultimedia = p.PublicacionOriginal.UrlMultimedia,
                        TipoMultimedia = p.PublicacionOriginal.TipoMultimedia,
                        Autor = new AutorResumenDto
                        {
                            Id = p.PublicacionOriginal.UsuarioId,
                            Username = p.PublicacionOriginal.Usuario.Username
                        }
                    } : null
                })
                .Take(limite + 1)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<PublicacionDetalleDto>> ObtenerPublicacionesPorIds(IReadOnlyCollection<int> publicacionesIds, CancellationToken cancellationToken = default)
        {
            if (publicacionesIds.Count == 0)
            {
                return new List<PublicacionDetalleDto>();
            }

            return await _context.Publicaciones
                .AsNoTracking()
                .Where(p =>
                    !p.EstaOculto &&
                    publicacionesIds.Contains(p.Id))
                .Select(p => new PublicacionDetalleDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,

                    Descripcion = p.Descripcion.Length > 300
                        ? p.Descripcion.Substring(0, 297) + "..."
                        : p.Descripcion,

                    Fecha = p.Fecha,
                    FechaCreacion = p.FechaCreación,
                    Categoria = p.Categoria,

                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,

                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    },

                    Lugar = new LugarDetalleDto
                    {
                        Id = p.LugarId,
                        Nombre = p.Lugar.Nombre,
                        Direccion = p.Lugar.Direccion,
                        Latitud = p.Lugar.Coordenadas.Y,
                        Longitud = p.Lugar.Coordenadas.X
                    },
                    CantidadLikes = 0,
                    CantidadComentarios = p.Comentarios.Count(c => !c.EstaOculto && !c.EstaEliminado),
                    DioLike = false,
                    Grupo = p.GrupoId.HasValue ? new GrupoResumenDto
                    {
                        Id = p.Grupo!.Id,
                        Nombre = p.Grupo.Nombre,
                        Descripcion = p.Grupo.Descripcion,
                        Decada = p.Grupo.Decada,
                        FechaCreacion = p.Grupo.FechaCreacion,
                        Creador = new AutorResumenDto
                        {
                            Id = p.Grupo.CreadorId,
                            Username = p.Grupo.Creador.Username
                        },
                        CantidadIntegrantes = p.Grupo.Integrantes.Count
                    } : null,
                    PublicacionOriginal = p.PublicacionOriginal != null ? new PublicacionResumenDto
                    {
                        Id = p.PublicacionOriginal.Id,
                        Titulo = p.PublicacionOriginal.Titulo,
                        DescripcionBreve = p.PublicacionOriginal.Descripcion.Length > 180
                            ? p.PublicacionOriginal.Descripcion.Substring(0, 180) + "..."
                            : p.PublicacionOriginal.Descripcion,
                        Fecha = p.PublicacionOriginal.Fecha,
                        Categoria = p.PublicacionOriginal.Categoria,
                        UrlMultimedia = p.PublicacionOriginal.UrlMultimedia,
                        TipoMultimedia = p.PublicacionOriginal.TipoMultimedia,
                        Autor = new AutorResumenDto
                        {
                            Id = p.PublicacionOriginal.UsuarioId,
                            Username = p.PublicacionOriginal.Usuario.Username
                        }
                    } : null
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<PublicacionDetalleDto>> ObtenerPorUsuarioId(int usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .AsNoTracking()
                .Where(p => !p.EstaOculto && p.UsuarioId == usuarioId)
                .OrderByDescending(p => p.FechaCreación)
                .ThenByDescending(p => p.Id)
                .Select(p => new PublicacionDetalleDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,
                    Descripcion = p.Descripcion,
                    Fecha = p.Fecha,
                    FechaCreacion = p.FechaCreación,
                    Categoria = p.Categoria,
                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,
                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    },
                    Lugar = new LugarDetalleDto
                    {
                        Id = p.LugarId,
                        Nombre = p.Lugar.Nombre,
                        Direccion = p.Lugar.Direccion,
                        Latitud = p.Lugar.Coordenadas.Y,
                        Longitud = p.Lugar.Coordenadas.X
                    },
                    CantidadLikes = 0,
                    CantidadComentarios = p.Comentarios.Count(c => !c.EstaOculto && !c.EstaEliminado),
                    DioLike = false,
                    Grupo = p.GrupoId.HasValue ? new GrupoResumenDto
                    {
                        Id = p.Grupo!.Id,
                        Nombre = p.Grupo.Nombre,
                        Descripcion = p.Grupo.Descripcion,
                        Decada = p.Grupo.Decada,
                        FechaCreacion = p.Grupo.FechaCreacion,
                        Creador = new AutorResumenDto
                        {
                            Id = p.Grupo.CreadorId,
                            Username = p.Grupo.Creador.Username
                        },
                        CantidadIntegrantes = p.Grupo.Integrantes.Count
                    } : null,
                    PublicacionOriginal = p.PublicacionOriginal != null ? new PublicacionResumenDto
                    {
                        Id = p.PublicacionOriginal.Id,
                        Titulo = p.PublicacionOriginal.Titulo,
                        DescripcionBreve = p.PublicacionOriginal.Descripcion.Length > 180
                            ? p.PublicacionOriginal.Descripcion.Substring(0, 180) + "..."
                            : p.PublicacionOriginal.Descripcion,
                        Fecha = p.PublicacionOriginal.Fecha,
                        Categoria = p.PublicacionOriginal.Categoria,
                        UrlMultimedia = p.PublicacionOriginal.UrlMultimedia,
                        TipoMultimedia = p.PublicacionOriginal.TipoMultimedia,
                        Autor = new AutorResumenDto
                        {
                            Id = p.PublicacionOriginal.UsuarioId,
                            Username = p.PublicacionOriginal.Usuario.Username
                        }
                    } : null
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<CategoriaEnum, int>> ObtenerCantidadPorCategoriaDelUsuario(int usuarioId, DateTime desde, 
            CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .AsNoTracking()
                .Where(p =>
                    p.UsuarioId == usuarioId &&
                    !p.EstaOculto &&
                    p.FechaCreación >= desde)
                .GroupBy(p => p.Categoria)
                .Select(grupo => new
                {
                    Categoria = grupo.Key,
                    Cantidad = grupo.Count()
                })
                .ToDictionaryAsync(
                    resultado => resultado.Categoria,
                    resultado => resultado.Cantidad,
                    cancellationToken);
        }

        public async Task<bool> Eliminar(int id, int? autorId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Publicaciones.Where(p => p.Id == id);
            if (autorId.HasValue && autorId.Value > 0)
            {
                query = query.Where(p => p.UsuarioId == autorId.Value);
            }

            var publicacion = await query.FirstOrDefaultAsync(cancellationToken);
            if (publicacion == null) return false;

            _context.Publicaciones.Remove(publicacion);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> EliminarRepost(int originalId, int? autorId = null, CancellationToken cancellationToken = default)
        {
            bool eliminado = false;
            if (autorId.HasValue && autorId.Value > 0)
            {
                eliminado = await QuitarRepost(originalId, autorId.Value, cancellationToken);
            }

            var query = _context.Publicaciones.Where(p => p.PublicacionOriginalId == originalId);
            if (autorId.HasValue && autorId.Value > 0)
            {
                query = query.Where(p => p.UsuarioId == autorId.Value);
            }

            var repostLegado = await query.OrderByDescending(p => p.FechaCreación).FirstOrDefaultAsync(cancellationToken);
            if (repostLegado != null)
            {
                _context.Publicaciones.Remove(repostLegado);
                await _context.SaveChangesAsync(cancellationToken);
                eliminado = true;
            }

            return eliminado;
        }
        public async Task<int> ContarPosteosCercanosAlUsuario(int usuarioId, NetTopologySuite.Geometries.Point gpsUsuario, CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .Where(p => p.UsuarioId == usuarioId &&
                            !p.EstaOculto &&
                            p.Lugar.Coordenadas.Distance(gpsUsuario) < 0.01) // ~10 cuadras
                .CountAsync(cancellationToken);
        }

        public async Task<int> ContarPosteosDeDiferentesEpocasDelUsuario(int usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .Where(p => p.UsuarioId == usuarioId && !p.EstaOculto)
                .Select(p => p.Fecha.Year / 10 * 10)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        public async Task<int> ContarPosteosConImagenDelUsuario(int usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .Where(p => p.UsuarioId == usuarioId && !p.EstaOculto && p.TipoMultimedia == MultimediaEnum.Foto)
                .CountAsync(cancellationToken);
        }

        public async Task<int> ContarRepostsDeOtrosUsuarios(int usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .Where(p => p.UsuarioId == usuarioId && !p.EstaOculto &&
                            p.PublicacionOriginalId.HasValue && 
                            p.PublicacionOriginal!.UsuarioId != usuarioId)
                .CountAsync(cancellationToken);
        }

        public async Task<int> ContarLikesOtorgados(int usuarioId, CancellationToken cancellationToken = default)
        {
            if (usuarioId <= 0) return 0;

            cancellationToken.ThrowIfCancellationRequested();
            await using var session = _neo4jDriver.AsyncSession();

            return await session.ExecuteReadAsync(async transaction =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var cursor = await transaction.RunAsync("""
                    MATCH (:Usuario { postgresId: $usuarioId })-[like:LE_GUSTA]->(:Publicacion)
                    RETURN count(like) AS totalLikes
                    """, new { usuarioId });

                 if (await cursor.FetchAsync())
                 {
                    return cursor.Current["totalLikes"].As<int>();
                 }

                return 0;
            });
        }
        public async Task<int> SumarPuntosOtorgadosPorUsuario(int usuarioId, CancellationToken cancellationToken = default)
        {
            if (usuarioId <= 0) return 0;

            cancellationToken.ThrowIfCancellationRequested();
            await using var session = _neo4jDriver.AsyncSession();

            return await session.ExecuteReadAsync(async transaction =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var cursor = await transaction.RunAsync(
                    SumarPuntosOtorgadosQuery,
                    new { usuarioId });

                if (await cursor.FetchAsync())
                {
                    return cursor.Current["totalPuntos"].As<int>();
                }

                return 0;
            });
        }

    }
}
