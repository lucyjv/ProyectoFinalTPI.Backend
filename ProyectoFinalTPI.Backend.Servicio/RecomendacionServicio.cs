using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class RecomendacionServicio(
    IPublicacionRepositorio publicacionRepositorio,
    IDriver neo4jDriver) : IRecomendacionServicio
{
    private const string RecomendacionesQuery = """
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
        WHERE candidata.postgresId IS NOT NULL
          AND NOT (usuario)-[:LE_GUSTA]->(candidata)
        RETURN candidata.postgresId AS publicacionId, sum(relevancia) AS puntaje
        ORDER BY puntaje DESC, publicacionId DESC
        LIMIT $cantidadMaxima
        """;

    public async Task<IReadOnlyList<int>> GenerarRecomendacionParaUsuario(
        int usuarioId, int cantidadMaxima, CancellationToken cancellationToken = default)
    {
        if (usuarioId <= 0)
            throw new ArgumentException("El ID del usuario debe ser mayor que cero.");
        if (cantidadMaxima < 1 || cantidadMaxima > 500)
            throw new ArgumentException("La cantidad máxima debe estar entre 1 y 500.");

        cancellationToken.ThrowIfCancellationRequested();
        var recomendaciones = await ObtenerIdsRelacionados(usuarioId, cantidadMaxima, cancellationToken);
        if (recomendaciones.Count == 0) return Array.Empty<int>();

        var publicacionesVisibles = await publicacionRepositorio.ObtenerPublicacionesPorIds(
            recomendaciones, cancellationToken);
        var visibles = publicacionesVisibles.Select(publicacion => publicacion.Id).ToHashSet();
        return recomendaciones.Where(visibles.Contains).ToArray();
    }

    private async Task<IReadOnlyList<int>> ObtenerIdsRelacionados(
        int usuarioId, int cantidadMaxima, CancellationToken cancellationToken)
    {
        await using var session = neo4jDriver.AsyncSession();
        return await session.ExecuteReadAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cursor = await transaction.RunAsync(
                RecomendacionesQuery,
                new { usuarioId, cantidadMaxima });
            var ids = new List<int>();
            await foreach (var registro in cursor)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ids.Add(registro["publicacionId"].As<int>());
            }
            return (IReadOnlyList<int>)ids;
        });
    }
}
