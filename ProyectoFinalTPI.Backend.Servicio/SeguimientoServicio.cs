using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
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

        private const string ContarSeguidosQuery = @"
            MATCH (:Usuario { postgresId: $idUsuario })-[:SIGUE_A]->(:Usuario)
            RETURN count(*) AS total";

        private const string ContarSeguidoresQuery = @"
            MATCH (:Usuario)-[:SIGUE_A]->(:Usuario { postgresId: $idUsuario })
            RETURN count(*) AS total";


        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IDriver _neo4jDriver;
        private readonly INotificacionServicio _notificacionServicio;
        private readonly ILogroServicio _logroServicio;

        public SeguimientoServicio(
            IUsuarioRepositorio usuarioRepositorio,
            IDriver neo4jDriver,
            ILogroServicio logroServicio,
            INotificacionServicio notificacionServicio)
        {
            _usuarioRepositorio = usuarioRepositorio;
            _neo4jDriver = neo4jDriver;
            _logroServicio = logroServicio;
            _notificacionServicio = notificacionServicio;
        }
        #region Operaciones públicas
        public async Task<ResultadoSeguimiento> SeguirUsuario(
            int idUsuario, int idUsuarioASeguir, CancellationToken cancellationToken = default)
        {
            var validacion = await ValidarUsuariosSeguimiento(idUsuario, idUsuarioASeguir, cancellationToken);
            if (validacion.HasValue) return validacion.Value;

            if (!await CrearRelacionNeo4jAsync(idUsuario, idUsuarioASeguir))
                return ResultadoSeguimiento.SinCambios;

            await _notificacionServicio.NotificarNuevoSeguidorAsync(idUsuarioASeguir, idUsuario, cancellationToken);
            await VerificarLogros(idUsuario, idUsuarioASeguir);

            return ResultadoSeguimiento.Aplicado;
        }

        public async Task<ResultadoSeguimiento> DejarDeSeguirUsuario(
            int idUsuario, int idUsuarioASeguir, CancellationToken cancellationToken = default)
        {
            var validacion = await ValidarUsuariosSeguimiento(idUsuario, idUsuarioASeguir, cancellationToken);
            if (validacion.HasValue) return validacion.Value;

            var relacionEliminada = await EliminarRelacionNeo4jAsync(idUsuario, idUsuarioASeguir);
            return relacionEliminada ? ResultadoSeguimiento.Aplicado : ResultadoSeguimiento.SinCambios;
        }

        public Task<IReadOnlyList<AutorResumenDto>?> ListarSeguidos(
            int idUsuario, CancellationToken cancellationToken = default) =>
            ListarUsuariosRelacionados(idUsuario, ListarSeguidosQuery, cancellationToken);

        public Task<IReadOnlyList<AutorResumenDto>?> ListarSeguidores(
            int idUsuario, CancellationToken cancellationToken = default) =>
            ListarUsuariosRelacionados(idUsuario, ListarSeguidoresQuery, cancellationToken);
        public async Task<IReadOnlyCollection<int>> ObtenerSeguidosIdsAsync(
            int usuarioId, CancellationToken cancellationToken = default)
        {
            await ValidarUsuarioParaConsulta(usuarioId, cancellationToken);
            return await ObtenerSeguidosIdsNeo4jAsync(usuarioId, cancellationToken);
        }
        #endregion

        #region Métodos privados y validaciones
        private async Task<int> ContarSeguidos(int idUsuario)
        {
            await using var session = _neo4jDriver.AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(ContarSeguidosQuery, new { idUsuario });
                if (await cursor.FetchAsync())
                {
                    return cursor.Current["total"].As<int>();
                }
                return 0;
            });
        }

        private async Task<int> ContarSeguidores(int idUsuario)
        {
            await using var session = _neo4jDriver.AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(ContarSeguidoresQuery, new { idUsuario });
                if (await cursor.FetchAsync())
                {
                    return cursor.Current["total"].As<int>();
                }
                return 0;
            });
        }
        private async Task<ResultadoSeguimiento?> ValidarUsuariosSeguimiento(
            int idUsuario, int idUsuarioASeguir, CancellationToken cancellationToken)
        {
            if (idUsuario == idUsuarioASeguir) return ResultadoSeguimiento.AutoSeguimiento;

            var usuarioExiste = await _usuarioRepositorio.ExisteUsuario(idUsuario, cancellationToken);
            var usuarioASeguirExiste = await _usuarioRepositorio.ExisteUsuario(idUsuarioASeguir, cancellationToken);
            return usuarioExiste && usuarioASeguirExiste ? null : ResultadoSeguimiento.UsuarioNoEncontrado;
        }
        private async Task<bool> CrearRelacionNeo4jAsync(int idUsuario, int idUsuarioASeguir)
        {

            var resultado = await EjecutarEscrituraRelacionNeo4jAsync(CrearRelacionQuery, idUsuario, idUsuarioASeguir);
            return resultado.Counters.RelationshipsCreated > 0;
        }
        private async Task<bool> EliminarRelacionNeo4jAsync(
            int idUsuario, int idUsuarioASeguir)
        {
            var resultado = await EjecutarEscrituraRelacionNeo4jAsync(EliminarRelacionQuery, idUsuario, idUsuarioASeguir);
            return resultado.Counters.RelationshipsDeleted > 0;
        }
        private async Task<IResultSummary> EjecutarEscrituraRelacionNeo4jAsync(
            string consulta, int idUsuario, int idUsuarioASeguir)
        {
            await using var session = _neo4jDriver.AsyncSession();
            return await session.ExecuteWriteAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(consulta, new { idUsuario, idUsuarioASeguir });
                return await cursor.ConsumeAsync();
            });
        }
        private async Task<IReadOnlyList<AutorResumenDto>?> ListarUsuariosRelacionados(
            int idUsuario, string consulta, CancellationToken cancellationToken)
        {
            if (!await _usuarioRepositorio.ExisteUsuario(idUsuario, cancellationToken)) return null;

            var ids = await ObtenerIdsRelacionadosNeo4jAsync(idUsuario, consulta);
            var usuarios = await _usuarioRepositorio.ObtenerUsuariosPorIdsAsync(ids, cancellationToken);
            var usuariosPorId = usuarios.ToDictionary(usuario => usuario.Id);
            return ids.Where(usuariosPorId.ContainsKey).Select(id => usuariosPorId[id]).ToList();
        }
        private async Task<List<int>> ObtenerIdsRelacionadosNeo4jAsync(int idUsuario, string consulta)
        {
            await using var session = _neo4jDriver.AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(consulta, new { idUsuario });
                var ids = new List<int>();
                await foreach (var registro in cursor)
                    ids.Add(registro["idUsuario"].As<int>());
                return ids;
            });
        }
        private async Task<List<int>> ObtenerSeguidosIdsNeo4jAsync(int usuarioId, CancellationToken cancellationToken)
        {
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
        private async Task ValidarUsuarioParaConsulta(int usuarioId, CancellationToken cancellationToken)
        {
            if (usuarioId <= 0)
                throw new ArgumentException("El ID del usuario debe ser mayor que cero.");
            if (!await _usuarioRepositorio.ExisteUsuario(usuarioId, cancellationToken))
                throw new KeyNotFoundException("El usuario indicado no existe.");
        }
        private async Task VerificarLogros(int seguidorId, int seguidoId)
        {
            var totalSeguidos = await ContarRelacionesNeo4jAsync(seguidorId, ContarSeguidosQuery);
            if (totalSeguidos >= 5)
                await _logroServicio.VerificarYDarLogro(seguidorId, LogroEnum.ConAmigos);

            var totalSeguidores = await ContarRelacionesNeo4jAsync(seguidoId, ContarSeguidoresQuery);
            if (totalSeguidores >= 50)
                await _logroServicio.VerificarYDarLogro(seguidoId, LogroEnum.Influencer);
        }
        private async Task<int> ContarRelacionesNeo4jAsync(int idUsuario, string consulta)
        {
            await using var session = _neo4jDriver.AsyncSession();
            return await session.ExecuteReadAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(consulta, new { idUsuario });
                return await cursor.FetchAsync() ? cursor.Current["total"].As<int>() : 0;
            });
        }
        #endregion
    }
}
