using Neo4j.Driver;
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

        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IPublicacionRepositorio _publicacionRepositorio;
        private readonly IDriver _neo4jDriver;

        public EtiquetadoPublicacionServicio( IUsuarioRepositorio usuarioRepositorio, IPublicacionRepositorio publicacionRepositorio,
            IDriver neo4jDriver)
        {
            _usuarioRepositorio = usuarioRepositorio;
            _publicacionRepositorio = publicacionRepositorio;
            _neo4jDriver = neo4jDriver;
        }

        //valida los IDs y ejecuta la consulta Cypher
        public async Task<int> EtiquetarUsuarios(
            int publicacionId, IReadOnlyCollection<int> usuariosIds,CancellationToken cancellationToken = default)
        {
            var ids = ValidarIds(publicacionId, usuariosIds);

            await ValidarPublicacionExistente(publicacionId, cancellationToken);
            await ValidarUsuariosExistentes(ids, cancellationToken);

            return await CrearRelacionesEnNeo4j(publicacionId, ids);
        }



        private static int[] ValidarIds(
            int publicacionId,IReadOnlyCollection<int> usuariosIds)
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
            int publicacionId, CancellationToken cancellationToken)
        {
            var existe = await _publicacionRepositorio.ExistePublicacionVisible( publicacionId,cancellationToken);

            if (!existe)
            {
                throw new KeyNotFoundException(
                    "La publicación no existe o no esta disponible");
            }
        }

        private async Task ValidarUsuariosExistentes(
            IReadOnlyCollection<int> usuariosIds, CancellationToken cancellationToken)
        {
            var idsExistentes = await _usuarioRepositorio.ObtenerIdsExistentesAsync( usuariosIds,cancellationToken);

            if (idsExistentes.Count == usuariosIds.Count)
            {
                return;
            }

            var idsFaltantes = usuariosIds.Except(idsExistentes);
            throw new KeyNotFoundException(
                $"No se encontraron los usuarios con ID: {string.Join(", ", idsFaltantes)}.");
        }

        private async Task<int> CrearRelacionesEnNeo4j(
            int publicacionId, IReadOnlyCollection<int> usuariosIds)
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
