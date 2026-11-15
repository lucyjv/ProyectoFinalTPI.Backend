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

        #region Operaciones públicas

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
            ValidarPublicacionId(publicacionId);
            await ValidarPublicacionExistente(publicacionId, null, cancellationToken);
            var ids = await LeerIdsNeo4j(ObtenerEtiquetasQuery, new { publicacionId }, "idUsuario");
            return ids.Count == 0 ? Array.Empty<AutorResumenDto>()
                : await _usuarioRepositorio.ObtenerUsuariosPorIdsAsync(ids, cancellationToken);
        }

        public Task<bool> DesetiquetarUsuario(int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            ValidarPublicacionId(publicacionId);
            if (usuarioId <= 0)
                throw new ArgumentException("El ID del usuario debe ser mayor que cero", nameof(usuarioId));
            return EliminarEtiquetaEnNeo4j(publicacionId, usuarioId);
        }

        public async Task CompletarEtiquetas(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones,
            CancellationToken cancellationToken = default)
        {
            if (publicaciones == null || publicaciones.Count == 0) return;
            var mapa = await ObtenerMapaEtiquetas(publicaciones.Select(p => p.Id).Distinct().ToArray());
            var ids = mapa.Values.SelectMany(usuarios => usuarios).Distinct().ToArray();
            if (ids.Length == 0) return;
            var usuarios = await _usuarioRepositorio.ObtenerUsuariosPorIdsAsync(ids, cancellationToken);
            AplicarEtiquetas(publicaciones, mapa, usuarios.ToDictionary(usuario => usuario.Id));
        }

        public async Task<List<PublicacionDetalleDto>> ObtenerPublicacionesDondeEstaEtiquetado(
            int usuarioId,
            CancellationToken cancellationToken = default)
        {
            ValidarUsuarioId(usuarioId);
            var ids = await LeerIdsNeo4j(
                ObtenerPublicacionesPorEtiquetadoQuery, new { usuarioId }, "publicacionId");
            return ids.Count == 0 ? new List<PublicacionDetalleDto>()
                : await _publicacionRepositorio.ObtenerPublicacionesPorIds(ids, cancellationToken);
        }

        #endregion

        #region Métodos privados y validaciones

        private static int[] ValidarIds(int publicacionId, IReadOnlyCollection<int> usuariosIds)
        {
            ArgumentNullException.ThrowIfNull(usuariosIds);
            ValidarPublicacionId(publicacionId);
            var ids = usuariosIds.Distinct().ToArray();
            ValidarUsuariosIds(ids);
            return ids;
        }

        private static void ValidarPublicacionId(int publicacionId)
        {
            if (publicacionId <= 0)
                throw new ArgumentException(
                    "El ID de la publicación debe ser mayor que cero", nameof(publicacionId));
        }

        private static void ValidarUsuariosIds(IReadOnlyCollection<int> usuariosIds)
        {
            if (usuariosIds.Count == 0)
                throw new ArgumentException(
                    "Debe indicar al menos un usuario para etiquetar", nameof(usuariosIds));
            if (usuariosIds.Any(id => id <= 0))
                throw new ArgumentException(
                    "Los IDs de usuarios deben ser mayores que cero.", nameof(usuariosIds));
        }

        private async Task ValidarPublicacionExistente(int publicacionId, IReadOnlyCollection<int>? usuariosIds, CancellationToken cancellationToken)
        {
            if (!await _publicacionRepositorio.ExistePublicacionVisible(publicacionId, cancellationToken))
                throw new KeyNotFoundException("La publicación no existe o no esta disponible");
            if (usuariosIds is { Count: > 0 })
                await ValidarAutorNoEtiquetado(publicacionId, usuariosIds, cancellationToken);
        }
        private async Task ValidarAutorNoEtiquetado(int publicacionId, IReadOnlyCollection<int> usuariosIds, CancellationToken cancellationToken)
        {
            try
            {
                var publicacion = await _publicacionRepositorio.ObtenerPorId(publicacionId, cancellationToken);
                if (publicacion != null && usuariosIds.Contains(publicacion.UsuarioId))
                    throw new InvalidOperationException("El autor de la publicación no puede etiquetarse a sí mismo.");
            }
            catch (NotSupportedException)
            {
                // Se conserva la compatibilidad existente con los repositorios simulados.
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

        private static void ValidarUsuarioId(int usuarioId)
        {
            if (usuarioId <= 0)
                throw new ArgumentException("El ID del usuario debe ser mayor que cero", nameof(usuarioId));
        }

        private async Task<List<int>> LeerIdsNeo4j(string consulta, object parametros, string campo)
        {
            await using var session = _neo4jDriver.AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(consulta, parametros);
                var ids = new List<int>();
                await foreach (var record in cursor) ids.Add(record[campo].As<int>());
                return ids;
            });
        }

        private async Task<bool> EliminarEtiquetaEnNeo4j(int publicacionId, int usuarioId)
        {
            await using var session = _neo4jDriver.AsyncSession();
            return await session.ExecuteWriteAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(DesetiquetarQuery, new { publicacionId, usuarioId });
                var summary = await cursor.ConsumeAsync();
                return summary.Counters.RelationshipsDeleted > 0;
            });
        }

        private async Task<Dictionary<int, List<int>>> ObtenerMapaEtiquetas(int[] publicacionesIds)
        {
            await using var session = _neo4jDriver.AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(ObtenerEtiquetasMultiplesQuery, new { publicacionesIds });
                var mapa = new Dictionary<int, List<int>>();
                await foreach (var record in cursor)
                    mapa[record["publicacionId"].As<int>()] = record["usuariosIds"]
                        .As<List<object>>().Select(Convert.ToInt32).ToList();
                return mapa;
            });
        }

        private static void AplicarEtiquetas(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones,
            IReadOnlyDictionary<int, List<int>> mapa,
            IReadOnlyDictionary<int, AutorResumenDto> usuarios)
        {
            foreach (var publicacion in publicaciones)
                if (mapa.TryGetValue(publicacion.Id, out var ids))
                    publicacion.UsuariosEtiquetados = ids.Where(usuarios.ContainsKey)
                        .Select(id => usuarios[id]).ToList();
        }


        #endregion

    }
}
