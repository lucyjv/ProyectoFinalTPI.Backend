using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
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

        // consulta nos devuelve las publicaciones con alto puntaje como candidatas al feed
        // busca publicaciones candidatas para el feed usando los likes y las etiquetas guardadas en Neo4j
        // Busca las publicaciones que le gustaron al usuario --> MATCH (usuario)-[:LE_GUSTA]->(semilla)
        // Busca a otros usuarios que también dieron like a la semilla y las publicaciones a las que ellos dieron like.
        //                  APORTA 2
        // Busca usuarios etiquetados en la semilla y otras publicaciones donde estén etiquetados.
        //                  APORTA 1
        // Descarta candidatos sin ID y publicaciones que el usuario ya marcó con like  
        private const string RecomendacionesPorLikesQuery = """
            MATCH (usuario:Usuario {postgresId: $usuarioId})-[:LE_GUSTA]->(semilla:Publicacion)
            CALL {
                WITH semilla
                MATCH (otroUsuario:Usuario)-[:LE_GUSTA]->(semilla)
                MATCH (otroUsuario)-[:LE_GUSTA]->(candidata:Publicacion)
                RETURN candidata, count(DISTINCT otroUsuario) * 2 AS relevancia
                UNION
                WITH semilla
                MATCH (etiquetado:Usuario)-[:ETIQUETADO_EN]->(semilla)
                MATCH (etiquetado)-[:ETIQUETADO_EN]->(candidata:Publicacion)
                RETURN candidata, count(DISTINCT etiquetado) AS relevancia
            }
            WITH usuario, candidata, relevancia
            WHERE candidata.postgresId IS NOT NULL
              AND NOT (usuario)-[:LE_GUSTA]->(candidata)
            RETURN candidata.postgresId AS publicacionId, sum(relevancia) AS puntaje
            ORDER BY puntaje DESC, publicacionId DESC
            LIMIT $cantidadMaxima
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

        public async Task<IReadOnlyList<int>> ObtenerPublicacionesRelacionadasPorLikes(
            int usuarioId, int cantidadMaxima, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = ObtenerNeo4jDriver().AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    RecomendacionesPorLikesQuery, new { usuarioId, cantidadMaxima });
                var ids = new List<int>();
                await foreach (var registro in cursor)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ids.Add(registro["publicacionId"].As<int>());
                }
                return (IReadOnlyList<int>)ids;
            });
        }

        private IDriver ObtenerNeo4jDriver() => _neo4jDriver ??
            throw new InvalidOperationException("El driver de Neo4j no esta configurado");

        public List<Publicacion> ObtenerTodasPublicaciones()
        {
            return _context.Publicaciones
                           .Where(p => !p.EstaOculto)
                           .Include(p => p.Usuario)
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
                .Where(p => !p.EstaOculto)
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
                    DioLike = false
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
                    DioLike = false
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

        public async Task<List<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(IReadOnlyCollection<CategoriaEnum> categorias, DateTime fechaCorte,
        CursorCronologicoDto? cursor, int limite, CancellationToken cancellationToken = default)
        {
            var consulta = _context.Publicaciones
                .AsNoTracking()
                .Where(p =>
                    !p.EstaOculto &&
                    p.FechaCreación <= fechaCorte);

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
                    DioLike = false
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
                    DioLike = false
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
            var query = _context.Publicaciones.Where(p => p.PublicacionOriginalId == originalId);
            if (autorId.HasValue && autorId.Value > 0)
            {
                query = query.Where(p => p.UsuarioId == autorId.Value);
            }

            var repost = await query.OrderByDescending(p => p.FechaCreación).FirstOrDefaultAsync(cancellationToken);
            if (repost == null)
            {
                // Por si se envió directamente el Id de la fila de repost
                repost = await _context.Publicaciones.FirstOrDefaultAsync(
                    p => p.Id == originalId && (autorId == null || p.UsuarioId == autorId.Value),
                    cancellationToken);
            }

            if (repost == null) return false;

            _context.Publicaciones.Remove(repost);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
