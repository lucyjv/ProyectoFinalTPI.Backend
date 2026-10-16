using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class MeGustaPublicacionServicio(
    IPublicacionRepositorio publicacionRepositorio,
    IUsuarioRepositorio usuarioRepositorio,
    IDriver neo4jDriver) : IMeGustaPublicacionServicio
{
    private const string DarLikeQuery = """
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

    private const string ObtenerEstadoQuery = """
        MATCH (publicacion:Publicacion {postgresId: $publicacionId})
        OPTIONAL MATCH (usuario:Usuario)-[like:LE_GUSTA]->(publicacion)
        RETURN count(like) AS cantidadLikes,
               count(CASE WHEN usuario.postgresId = $usuarioId THEN 1 END) > 0 AS dioLike
        """;

    public async Task<MeGustaPublicacionDto> DarLike(
        int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
    {
        await ValidarInteraccion(publicacionId, usuarioId, cancellationToken);
        await EjecutarEscritura(DarLikeQuery, publicacionId, usuarioId, cancellationToken);
        return await ObtenerEstado(publicacionId, usuarioId, cancellationToken);
    }

    public async Task<MeGustaPublicacionDto> QuitarLike(
        int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
    {
        await ValidarInteraccion(publicacionId, usuarioId, cancellationToken);
        await EjecutarEscritura(QuitarLikeQuery, publicacionId, usuarioId, cancellationToken);
        return await ObtenerEstado(publicacionId, usuarioId, cancellationToken);
    }

    public async Task CompletarResumenes(
        IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (publicaciones.Count == 0) return;
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = neo4jDriver.AsyncSession();
        var resumenes = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("""
                UNWIND $publicacionesIds AS publicacionId
                OPTIONAL MATCH (usuario:Usuario)-[like:LE_GUSTA]->
                              (:Publicacion {postgresId: publicacionId})
                RETURN publicacionId,
                       count(like) AS cantidadLikes,
                       count(CASE WHEN usuario.postgresId = $usuarioId THEN 1 END) > 0 AS dioLike
                """, new
            {
                publicacionesIds = publicaciones.Select(p => p.Id).Distinct().ToArray(),
                usuarioId
            });
            var resultado = new Dictionary<int, (int Cantidad, bool DioLike)>();
            await foreach (var record in cursor)
                resultado[record["publicacionId"].As<int>()] =
                    (record["cantidadLikes"].As<int>(), record["dioLike"].As<bool>());
            return resultado;
        });

        foreach (var publicacion in publicaciones)
            if (resumenes.TryGetValue(publicacion.Id, out var resumen))
            {
                publicacion.CantidadLikes = resumen.Cantidad;
                publicacion.DioLike = resumen.DioLike;
            }
    }

    private async Task EjecutarEscritura(
        string query, int publicacionId, int usuarioId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = neo4jDriver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(query, new { publicacionId, usuarioId });
        });
    }

    private async Task ValidarInteraccion(
        int publicacionId, int usuarioId, CancellationToken cancellationToken)
    {
        ValidarId(publicacionId, "publicación");
        ValidarId(usuarioId, "usuario");
        if (!await usuarioRepositorio.ExisteUsuario(usuarioId, cancellationToken))
            throw new KeyNotFoundException("El usuario autenticado ya no existe.");
        if (!await publicacionRepositorio.ExistePublicacionVisible(publicacionId, cancellationToken))
            throw new KeyNotFoundException("La publicación no existe o no está disponible.");
    }

    private async Task<MeGustaPublicacionDto> ObtenerEstado(
        int publicacionId, int usuarioId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = neo4jDriver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(ObtenerEstadoQuery, new { publicacionId, usuarioId });
            var record = await cursor.SingleAsync();
            return new MeGustaPublicacionDto
            {
                PublicacionId = publicacionId,
                CantidadLikes = record["cantidadLikes"].As<int>(),
                DioLike = record["dioLike"].As<bool>()
            };
        });
    }

    private static void ValidarId(int id, string recurso)
    {
        if (id <= 0) throw new ArgumentException($"El ID de {recurso} debe ser mayor que cero.");
    }
}
