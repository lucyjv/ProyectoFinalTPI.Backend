using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Data;

namespace ProyectoFinalTPI.Backend.Services
{
    public class SeguimientoService : ISeguimientoService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IDriver _neo4jDriver;

        public SeguimientoService(ApplicationDbContext dbContext, IDriver neo4jDriver)
        {
            _dbContext = dbContext;
            _neo4jDriver = neo4jDriver;
        }




        // valida que existan ambos usuarios y rechaza el auto seguimiento.
        // si son validos, usa Neo4j para crear (si todavia no existen) sus nodos Usuario y la relacion SIGUE_A.
        // si ya se seguian, no duplica la relacion y devuelve un resultado distinto.
        public async Task<ResultadoSeguirUsuario> SeguirUsuarioAsync( int idUsuario, int idUsuarioASeguir, CancellationToken cancellationToken = default)
        {
            if (idUsuario == idUsuarioASeguir)
            {
                return ResultadoSeguirUsuario.AutoSeguimiento;
            }

            var usuariosEncontrados = await _dbContext.Usuario
                .Where(usuario => usuario.Id == idUsuario || usuario.Id == idUsuarioASeguir)
                .Select(usuario => usuario.Id)
                .Distinct()
                .CountAsync(cancellationToken);

            if (usuariosEncontrados != 2)
            {
                return ResultadoSeguirUsuario.UsuarioNoEncontrado;
            }

            const string query = @"
                MERGE (seguidor:Usuario { postgresId: $idUsuario })
                MERGE (seguido:Usuario { postgresId: $idUsuarioASeguir })
                MERGE (seguidor)-[relacion:SIGUE_A]->(seguido)
                ON CREATE SET relacion.desde = datetime()";

            await using var session = _neo4jDriver.AsyncSession();

            var relacionCreada = await session.ExecuteWriteAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(query, new
                {
                    idUsuario,
                    idUsuarioASeguir
                });

                var summary = await cursor.ConsumeAsync();
                return summary.Counters.RelationshipsCreated > 0;
            });

            return relacionCreada
                ? ResultadoSeguirUsuario.Seguido
                : ResultadoSeguirUsuario.YaLoSeguía;
        }
       
    }
}
