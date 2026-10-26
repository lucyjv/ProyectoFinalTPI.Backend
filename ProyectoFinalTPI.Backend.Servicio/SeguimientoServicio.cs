using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class SeguimientoServicio : ISeguimientoServicio
    {
        private const string CrearRelacionQuery = @"
            MERGE (seguidor:Usuario { postgresId: $idUsuario })
            MERGE (seguido:Usuario { postgresId: $idUsuarioASeguir })
            MERGE (seguidor)-[relacion:SIGUE_A]->(seguido)
            ON CREATE SET relacion.desde = datetime()";

        private const string EliminarRelacionQuery = @"
            MATCH (seguidor:Usuario { postgresId: $idUsuario })-[relacion:SIGUE_A]->
                  (seguido:Usuario { postgresId: $idUsuarioASeguir })
            DELETE relacion";


        private const string ObtenerSeguidosQuery = """
            MATCH (:Usuario { postgresId: $usuarioId })
                  -[:SIGUE_A]->(seguido:Usuario)
            WHERE seguido.postgresId IS NOT NULL
            RETURN DISTINCT seguido.postgresId AS id
            """;

        private const string ListarSeguidosQuery = @"
            MATCH (:Usuario { postgresId: $idUsuario })-[:SIGUE_A]->(seguido:Usuario)
            RETURN seguido.postgresId AS idUsuario
            ORDER BY idUsuario";

        private const string ListarSeguidoresQuery = @"
            MATCH (seguidor:Usuario)-[:SIGUE_A]->(:Usuario { postgresId: $idUsuario })
            RETURN seguidor.postgresId AS idUsuario
            ORDER BY idUsuario";


        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IDriver _neo4jDriver;

        public SeguimientoServicio(
            IUsuarioRepositorio usuarioRepositorio,
            IDriver neo4jDriver)
        {
            _usuarioRepositorio = usuarioRepositorio;
            _neo4jDriver = neo4jDriver;
        }

        public async Task<ResultadoSeguimiento> SeguirUsuario(
            int idUsuario, int idUsuarioASeguir, CancellationToken cancellationToken = default)
        {
            if (idUsuario == idUsuarioASeguir)
            {
                return ResultadoSeguimiento.AutoSeguimiento;
            }

            // valida en el repo
            var ambosUsuariosExisten = await AmbosUsuariosExistenAsync( idUsuario,idUsuarioASeguir,cancellationToken);

            if (!ambosUsuariosExisten)
            {
                return ResultadoSeguimiento.UsuarioNoEncontrado;
            }

            // creamos la relación en neo 
            var relacionCreada = await CrearRelacionNeo4jAsync(
                idUsuario,
                idUsuarioASeguir);

            return relacionCreada
                ? ResultadoSeguimiento.Aplicado
                : ResultadoSeguimiento.SinCambios;
        }

        public async Task<ResultadoSeguimiento> DejarDeSeguirUsuario(
            int idUsuario, int idUsuarioASeguir, CancellationToken cancellationToken = default)
        {
            if (idUsuario == idUsuarioASeguir)
            {
                return ResultadoSeguimiento.AutoSeguimiento;
            }

            var ambosUsuariosExisten = await AmbosUsuariosExistenAsync( idUsuario, idUsuarioASeguir, cancellationToken);

            if (!ambosUsuariosExisten)
            {
                return ResultadoSeguimiento.UsuarioNoEncontrado;
            }

            var relacionEliminada = await EliminarRelacionNeo4jAsync(
                idUsuario,
                idUsuarioASeguir);

            return relacionEliminada
                ? ResultadoSeguimiento.Aplicado
                : ResultadoSeguimiento.SinCambios;
        }

        public async Task<IReadOnlyList<AutorResumenDto>?> ListarSeguidos(
            int idUsuario, CancellationToken cancellationToken = default)
        {
            var usuarioExiste = await _usuarioRepositorio.ExisteUsuario(
                idUsuario, cancellationToken);

            if (!usuarioExiste)
            {
                return null;
            }

            // consulta las relaciones saliente "SIGUE_A" en neo
            var idsSeguidos = await ObtenerIdsSeguidosNeo4jAsync(idUsuario);
            var usuariosSeguidos = await _usuarioRepositorio.ObtenerUsuariosPorIdsAsync( idsSeguidos, cancellationToken);
            var usuariosPorId = usuariosSeguidos.ToDictionary(usuario => usuario.Id);

            return idsSeguidos
                .Where(usuariosPorId.ContainsKey)
                .Select(id => usuariosPorId[id])
                .ToList();
        }

        public async Task<IReadOnlyList<AutorResumenDto>?> ListarSeguidores(
            int idUsuario, CancellationToken cancellationToken = default)
        {
            var usuarioExiste = await _usuarioRepositorio.ExisteUsuario( idUsuario, cancellationToken);

            if (!usuarioExiste)
            {
                return null;
            }

            // consulta las relaciones entrantes "SIGUE_A" en neo
            var idsSeguidores = await ObtenerIdsSeguidoresNeo4jAsync(idUsuario);
            var usuariosSeguidores = await _usuarioRepositorio.ObtenerUsuariosPorIdsAsync(
                idsSeguidores,
                cancellationToken);
            var usuariosPorId = usuariosSeguidores.ToDictionary(usuario => usuario.Id);

            return idsSeguidores
                .Where(usuariosPorId.ContainsKey)
                .Select(id => usuariosPorId[id])
                .ToList();
        }

        private async Task<bool> AmbosUsuariosExistenAsync(
            int idUsuario, int idUsuarioASeguir, CancellationToken cancellationToken)
        {
            var usuarioExiste = await _usuarioRepositorio.ExisteUsuario(
                idUsuario,
                cancellationToken);

            var usuarioASeguirExiste = await _usuarioRepositorio.ExisteUsuario(
                idUsuarioASeguir,
                cancellationToken);

            return usuarioExiste && usuarioASeguirExiste;
        }

        private async Task<bool> CrearRelacionNeo4jAsync( int idUsuario, int idUsuarioASeguir)
        {

            //abre una sesion con Neo4j
            await using var session = _neo4jDriver.AsyncSession();

            // ejecuta la escritura en Neo4j
            return await session.ExecuteWriteAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    CrearRelacionQuery, // la query Cypher para crear la relacion
                    new { idUsuario, idUsuarioASeguir });

                var summary = await cursor.ConsumeAsync();
                return summary.Counters.RelationshipsCreated > 0;
            });
        }

        private async Task<bool> EliminarRelacionNeo4jAsync(
            int idUsuario, int idUsuarioASeguir)
        {
            await using var session = _neo4jDriver.AsyncSession();

            return await session.ExecuteWriteAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    EliminarRelacionQuery,
                    new { idUsuario, idUsuarioASeguir });

                var summary = await cursor.ConsumeAsync();
                return summary.Counters.RelationshipsDeleted > 0;
            });
        }


        public async Task<IReadOnlyCollection<int>> ObtenerSeguidosIdsAsync(int usuarioId, CancellationToken cancellationToken = default)
        {
            if (usuarioId <= 0)
            {
                throw new ArgumentException(
                    "El ID del usuario debe ser mayor que cero.");
            }

            var existeUsuario = await _usuarioRepositorio.ExisteUsuario(
                usuarioId,
                cancellationToken);

            if (!existeUsuario)
            {
                throw new KeyNotFoundException(
                    "El usuario indicado no existe.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            await using var session = _neo4jDriver.AsyncSession();

            var ids = await session.ExecuteReadAsync(async transaction =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resultado = await transaction.RunAsync(
                    ObtenerSeguidosQuery,
                    new { usuarioId });

                var seguidos = new List<int>();

                while (await resultado.FetchAsync())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    seguidos.Add(
                        resultado.Current["id"].As<int>());
                }

                return seguidos;
            });

            cancellationToken.ThrowIfCancellationRequested();

            return ids;
        }
        private async Task<List<int>> ObtenerIdsSeguidosNeo4jAsync(int idUsuario)
        {
            await using var session = _neo4jDriver.AsyncSession();

            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    ListarSeguidosQuery,
                    new { idUsuario });

                var idsSeguidos = new List<int>();
                await foreach (var registro in cursor)
                {
                    idsSeguidos.Add(registro["idUsuario"].As<int>());
                }

                return idsSeguidos;
            });
        }
        private async Task<List<int>> ObtenerIdsSeguidoresNeo4jAsync(int idUsuario)
        {
            await using var session = _neo4jDriver.AsyncSession();

            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    ListarSeguidoresQuery,
                    new { idUsuario });

                var idsSeguidores = new List<int>();
                await foreach (var registro in cursor)
                {
                    idsSeguidores.Add(registro["idUsuario"].As<int>());
                }

                return idsSeguidores;
            });

        }
    }
}
