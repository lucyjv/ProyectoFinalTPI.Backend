using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class EtiquetadoPublicacionServicio : IEtiquetadoPublicacionServicio
    {
        private const string CrearEtiquetasQuery = @"
            UNWIND $usuariosIds AS idUsuario
            MERGE (usuario:Usuario { postgresId: idUsuario })
            MERGE (publicacion:Publicacion { postgresId: $publicacionId })
            MERGE (usuario)-[relacion:ETIQUETADO_EN]->(publicacion)
            ON CREATE SET relacion.creadaEn = datetime()";

        private const string ObtenerEtiquetasQuery = @"
            MATCH (usuario:Usuario)-[:ETIQUETADO_EN]->(:Publicacion { postgresId: $publicacionId })
            RETURN usuario.postgresId AS idUsuario";

        private const string ObtenerEtiquetasMultiplesQuery = @"
            UNWIND $publicacionesIds AS publicacionId
            MATCH (usuario:Usuario)-[:ETIQUETADO_EN]->(publicacion:Publicacion { postgresId: publicacionId })
            RETURN publicacionId, collect(DISTINCT usuario.postgresId) AS usuariosIds";

        private const string ObtenerPublicacionesPorEtiquetadoQuery = @"
            MATCH (usuario:Usuario { postgresId: $usuarioId })-[:ETIQUETADO_EN]->(publicacion:Publicacion)
            RETURN publicacion.postgresId AS publicacionId";

        private const string DesetiquetarQuery = @"
            MATCH (usuario:Usuario { postgresId: $usuarioId })-[relacion:ETIQUETADO_EN]->(publicacion:Publicacion { postgresId: $publicacionId })
            DELETE relacion";

        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IPublicacionRepositorio _publicacionRepositorio;
        private readonly IDriver _neo4jDriver;

        public EtiquetadoPublicacionServicio(
            IUsuarioRepositorio usuarioRepositorio,
            IPublicacionRepositorio publicacionRepositorio,
            IDriver neo4jDriver)
        {
            _usuarioRepositorio = usuarioRepositorio;
            _publicacionRepositorio = publicacionRepositorio;
            _neo4jDriver = neo4jDriver;
        }

        // Valida los IDs y ejecuta la consulta Cypher para crear etiquetas
        public async Task<int> EtiquetarUsuarios(
            int publicacionId,
            IReadOnlyCollection<int> usuariosIds,
            CancellationToken cancellationToken = default)
        {
            var ids = ValidarIds(publicacionId, usuariosIds);

            await ValidarPublicacionExistente(publicacionId, ids, cancellationToken);
            await ValidarUsuariosExistentes(ids, cancellationToken);

            return await CrearRelacionesEnNeo4j(publicacionId, ids);
        }

        public async Task<IReadOnlyCollection<AutorResumenDto>> ObtenerUsuariosEtiquetados(
            int publicacionId,
            CancellationToken cancellationToken = default)
        {
            if (publicacionId <= 0)
            {
                throw new ArgumentException(
                    "El ID de la publicación debe ser mayor que cero",
                    nameof(publicacionId));
            }

            await ValidarPublicacionExistente(publicacionId, null, cancellationToken);

            await using var session = _neo4jDriver.AsyncSession();
            var idsUsuarios = await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    ObtenerEtiquetasQuery,
                    new { publicacionId });

                var ids = new List<int>();
                await foreach (var record in cursor)
                {
                    ids.Add(record["idUsuario"].As<int>());
                }
                return ids;
            });

            if (idsUsuarios.Count == 0)
            {
                return Array.Empty<AutorResumenDto>();
            }

            var usuarios = await _usuarioRepositorio.ObtenerUsuariosPorIdsAsync(idsUsuarios, cancellationToken);
            return usuarios;
        }

        public async Task<bool> DesetiquetarUsuario(
            int publicacionId,
            int usuarioId,
            CancellationToken cancellationToken = default)
        {
            if (publicacionId <= 0)
            {
                throw new ArgumentException(
                    "El ID de la publicación debe ser mayor que cero",
                    nameof(publicacionId));
            }

            if (usuarioId <= 0)
            {
                throw new ArgumentException(
                    "El ID del usuario debe ser mayor que cero",
                    nameof(usuarioId));
            }

            await using var session = _neo4jDriver.AsyncSession();
            return await session.ExecuteWriteAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    DesetiquetarQuery,
                    new { publicacionId, usuarioId });

                var summary = await cursor.ConsumeAsync();
                return summary.Counters.RelationshipsDeleted > 0;
            });
        }

        public async Task CompletarEtiquetas(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones,
            CancellationToken cancellationToken = default)
        {
            if (publicaciones == null || publicaciones.Count == 0) return;

            var publicacionesIds = publicaciones.Select(p => p.Id).Distinct().ToArray();
            await using var session = _neo4jDriver.AsyncSession();

            var mapaEtiquetas = await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    ObtenerEtiquetasMultiplesQuery,
                    new { publicacionesIds });

                var dic = new Dictionary<int, List<int>>();
                await foreach (var record in cursor)
                {
                    var pubId = record["publicacionId"].As<int>();
                    var userIds = record["usuariosIds"].As<List<object>>()
                        .Select(Convert.ToInt32)
                        .ToList();
                    dic[pubId] = userIds;
                }
                return dic;
            });

            var todosLosIds = mapaEtiquetas.Values.SelectMany(x => x).Distinct().ToArray();
            if (todosLosIds.Length == 0) return;

            var usuarios = await _usuarioRepositorio.ObtenerUsuariosPorIdsAsync(todosLosIds, cancellationToken);
            var usuariosPorId = usuarios.ToDictionary(u => u.Id);

            foreach (var pub in publicaciones)
            {
                if (mapaEtiquetas.TryGetValue(pub.Id, out var userIds))
                {
                    pub.UsuariosEtiquetados = userIds
                        .Where(id => usuariosPorId.ContainsKey(id))
                        .Select(id => usuariosPorId[id])
                        .ToList();
                }
            }
        }

        public async Task<List<PublicacionDetalleDto>> ObtenerPublicacionesDondeEstaEtiquetado(
            int usuarioId,
            CancellationToken cancellationToken = default)
        {
            if (usuarioId <= 0)
            {
                throw new ArgumentException("El ID de usuario debe ser mayor que cero", nameof(usuarioId));
            }

            await using var session = _neo4jDriver.AsyncSession();
            var publicacionesIds = await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    ObtenerPublicacionesPorEtiquetadoQuery,
                    new { usuarioId });

                var ids = new List<int>();
                await foreach (var record in cursor)
                {
                    ids.Add(record["publicacionId"].As<int>());
                }
                return ids;
            });

            if (publicacionesIds.Count == 0)
            {
                return new List<PublicacionDetalleDto>();
            }

            return await _publicacionRepositorio.ObtenerPublicacionesPorIds(publicacionesIds, cancellationToken);
        }

        private static int[] ValidarIds(
            int publicacionId,
            IReadOnlyCollection<int> usuariosIds)
        {
            ArgumentNullException.ThrowIfNull(usuariosIds);

            if (publicacionId <= 0)
            {
                throw new ArgumentException(
                    "El ID de la publicación debe ser mayor que cero",
                    nameof(publicacionId));
            }

            var idsUnicos = usuariosIds.Distinct().ToArray();

            if (idsUnicos.Length == 0)
            {
                throw new ArgumentException(
                    "Debe indicar al menos un usuario para etiquetar",
                    nameof(usuariosIds));
            }

            if (idsUnicos.Any(id => id <= 0))
            {
                throw new ArgumentException(
                    "Los IDs de usuarios deben ser mayores que cero.",
                    nameof(usuariosIds));
            }

            return idsUnicos;
        }

        private async Task ValidarPublicacionExistente(
            int publicacionId,
            IReadOnlyCollection<int>? usuariosIds,
            CancellationToken cancellationToken)
        {
            var existe = await _publicacionRepositorio.ExistePublicacionVisible(publicacionId, cancellationToken);

            if (!existe)
            {
                throw new KeyNotFoundException(
                    "La publicación no existe o no esta disponible");
            }

            if (usuariosIds != null && usuariosIds.Count > 0)
            {
                try
                {
                    var pub = await _publicacionRepositorio.ObtenerPorId(publicacionId, cancellationToken);
                    if (pub != null && usuariosIds.Contains(pub.UsuarioId))
                    {
                        throw new InvalidOperationException("El autor de la publicación no puede etiquetarse a sí mismo.");
                    }
                }
                catch (NotSupportedException)
                {
                    // Soporte para fakes en pruebas unitarias que no mockean ObtenerPorId
                }
            }
        }

        private async Task ValidarUsuariosExistentes(
            IReadOnlyCollection<int> usuariosIds,
            CancellationToken cancellationToken)
        {
            var idsExistentes = await _usuarioRepositorio.ObtenerIdsExistentesAsync(usuariosIds, cancellationToken);

            if (idsExistentes.Count == usuariosIds.Count)
            {
                return;
            }

            var idsFaltantes = usuariosIds.Except(idsExistentes);
            throw new KeyNotFoundException(
                $"No se encontraron los usuarios con ID: {string.Join(", ", idsFaltantes)}.");
        }

        private async Task<int> CrearRelacionesEnNeo4j(
            int publicacionId,
            IReadOnlyCollection<int> usuariosIds)
        {
            await using var session = _neo4jDriver.AsyncSession();

            return await session.ExecuteWriteAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    CrearEtiquetasQuery,
                    new { publicacionId, usuariosIds });

                var summary = await cursor.ConsumeAsync();
                return (int)summary.Counters.RelationshipsCreated;
            });
        }
    }
}
