using Neo4j.Driver;
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

        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IDriver _neo4jDriver;

        public SeguimientoServicio( IUsuarioRepositorio usuarioRepositorio, IDriver neo4jDriver)
        {
            _usuarioRepositorio = usuarioRepositorio;
            _neo4jDriver = neo4jDriver;
        }

        public async Task<ResultadoSeguirUsuario> SeguirUsuarioAsync
            ( int idUsuario, int idUsuarioASeguir,CancellationToken cancellationToken = default)
        {
            if (idUsuario == idUsuarioASeguir)
            {
                return ResultadoSeguirUsuario.AutoSeguimiento;
            }

            var ambosUsuariosExisten = await AmbosUsuariosExisten(
                idUsuario,
                idUsuarioASeguir,
                cancellationToken);

            if (!ambosUsuariosExisten)
            {
                return ResultadoSeguirUsuario.UsuarioNoEncontrado;
            }

            var relacionCreada = await CrearRelacionNeo4j(
                idUsuario,
                idUsuarioASeguir);

            return relacionCreada
                ? ResultadoSeguirUsuario.Seguido
                : ResultadoSeguirUsuario.YaLoSeguía;
        }

        private async Task<bool> AmbosUsuariosExisten
            ( int idUsuario,int idUsuarioASeguir,CancellationToken cancellationToken)
        {
            var usuarioExiste = await _usuarioRepositorio.ExisteUsuario(
                idUsuario,
                cancellationToken);

            var usuarioASeguirExiste = await _usuarioRepositorio.ExisteUsuario(
                idUsuarioASeguir,
                cancellationToken);

            return usuarioExiste && usuarioASeguirExiste;
        }

        private async Task<bool> CrearRelacionNeo4j( int idUsuario, int idUsuarioASeguir)
        {
            await using var session = _neo4jDriver.AsyncSession();

            return await session.ExecuteWriteAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(
                    CrearRelacionQuery,new { idUsuario, idUsuarioASeguir });

                var summary = await cursor.ConsumeAsync();
                return summary.Counters.RelationshipsCreated > 0;
            });
        }
    }
}
