using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class AdminEstadisticaRepositorio(ApplicationDbContext context, IDriver? neo4jDriver = null)
    : IAdminEstadisticaRepositorio
{
    public async Task<EstadisticasAdminDto> Obtener(int limiteRanking, CancellationToken cancellationToken = default)
    {
        var resultado = new EstadisticasAdminDto
        {
            TotalUsuarios = await context.Usuarios.CountAsync(u => !u.EstaEliminado, cancellationToken),
            TotalPublicaciones = await context.Publicaciones.CountAsync(cancellationToken),
            PublicacionesVisibles = await context.Publicaciones.CountAsync(p => !p.EstaOculto, cancellationToken),
            PublicacionesOcultas = await context.Publicaciones.CountAsync(p => p.EstaOculto, cancellationToken),
            PublicacionesPorCategoria = await context.Publicaciones.AsNoTracking().GroupBy(p => p.Categoria)
                .Select(g => new CategoriaEstadisticaDto { Categoria = g.Key.ToString(), Cantidad = g.Count() })
                .OrderByDescending(x => x.Cantidad).ToListAsync(cancellationToken)
        };
        resultado.MasGustadas = await ObtenerRanking("LE_GUSTA", limiteRanking, cancellationToken);
        resultado.MasPuntuadas = await ObtenerRanking("PUNTUO", limiteRanking, cancellationToken);
        return resultado;
    }

    private async Task<List<PublicacionRankingDto>> ObtenerRanking(
        string relacion, int limite, CancellationToken cancellationToken)
    {
        if (neo4jDriver is null) return [];
        var metricas = await ObtenerMetricasNeo4j(relacion, limite * 10, cancellationToken);
        var ids = metricas.Select(x => x.Id).ToArray();
        var publicaciones = await context.Publicaciones.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && !p.EstaOculto)
            .Select(p => new { p.Id, p.Titulo, p.Usuario.Username }).ToListAsync(cancellationToken);
        var datos = publicaciones.ToDictionary(p => p.Id);
        return metricas.Where(x => datos.ContainsKey(x.Id)).Take(limite).Select(x => new PublicacionRankingDto
        {
            PublicacionId = x.Id, Titulo = datos[x.Id].Titulo,
            AutorUsername = datos[x.Id].Username, Cantidad = x.Cantidad
        }).ToList();
    }

    private async Task<List<(int Id, int Cantidad)>> ObtenerMetricasNeo4j(
        string relacion, int limite, CancellationToken cancellationToken)
    {
        var expresion = relacion == "PUNTUO" ? "sum(r.puntaje)" : "count(r)";
        var consulta = $"MATCH ()-[r:{relacion}]->(p:Publicacion) RETURN p.postgresId AS id, {expresion} AS cantidad ORDER BY cantidad DESC LIMIT $limite";
        await using var sesion = neo4jDriver!.AsyncSession();
        return await sesion.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(consulta, new { limite });
            var items = new List<(int, int)>();
            await foreach (var registro in cursor)
            {
                cancellationToken.ThrowIfCancellationRequested();
                items.Add((registro["id"].As<int>(), checked((int)registro["cantidad"].As<long>())));
            }
            return items;
        });
    }
}
