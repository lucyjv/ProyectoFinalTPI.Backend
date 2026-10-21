using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class RelacionRecuerdosRepositorio(ApplicationDbContext db) : IRelacionRecuerdosRepositorio
{
    public async Task<int?> ObtenerOriginalVisible(int id, CancellationToken ct)
    {
        var origen = await db.Publicaciones.AsNoTracking()
            .Where(p => p.Id == id && !p.EstaOculto)
            .Select(p => new { p.Id, p.PublicacionOriginalId }).SingleOrDefaultAsync(ct);
        if (origen == null) return null;
        if (origen.PublicacionOriginalId == null) return origen.Id;
        return await db.Publicaciones.Where(p => p.Id == origen.PublicacionOriginalId && !p.EstaOculto)
            .Select(p => (int?)p.Id).SingleOrDefaultAsync(ct);
    }

    public Task<List<RecuerdoRelacionadoDto>> Obtener(int id, string tipo, CursorRelacionDto? cursor, int limite, CancellationToken ct)
    {
        var distancia = cursor?.DistanciaMetros ?? -1;
        var ultimoId = cursor?.Id ?? 0;
        var soloDia = tipo == "dia";
        // geography expresa metros; AT TIME ZONE evita depender del huso de la sesión.
        // ST_DWithin preselecciona con un margen numérico de un micrómetro.
        // El filtro definitivo usa la misma distancia <= 200 que se devuelve y pagina:
        // evita excluir el borde por diferencias numéricas entre ambas funciones.
        // Todas las interpolaciones se envían como parámetros, nunca como fragmentos SQL.
        return db.Database.SqlQuery<RecuerdoRelacionadoDto>($"""
            SELECT p."Id", p."Titulo", left(p."Descripcion", 180) AS "DescripcionBreve",
                   p."Fecha", p."UrlMultimedia", p."TipoMultimedia",
                   p."UsuarioId" AS "AutorId", u."Username" AS "AutorUsername",
                   l."Nombre" AS "NombreLugar", d.metros AS "DistanciaMetros",
                   ((p."Fecha" AT TIME ZONE 'UTC')::date = (o."Fecha" AT TIME ZONE 'UTC')::date) AS "MismoDia"
            FROM "Publicaciones" o
            JOIN lugares origen ON origen."Id" = o."LugarId"
            JOIN lugares l ON ST_DWithin(l."Coordenadas"::geography, origen."Coordenadas"::geography, 200.000001)
            JOIN "Publicaciones" p ON p."LugarId" = l."Id"
            JOIN "Usuario" u ON u."Id" = p."UsuarioId"
            CROSS JOIN LATERAL (SELECT ST_Distance(l."Coordenadas"::geography, origen."Coordenadas"::geography) AS metros) d
            WHERE o."Id" = {id} AND NOT o."EstaOculto"
              AND d.metros <= 200
              AND p."Id" <> o."Id" AND NOT p."EstaOculto" AND p."PublicacionOriginalId" IS NULL
              AND (NOT {soloDia} OR (p."Fecha" AT TIME ZONE 'UTC')::date = (o."Fecha" AT TIME ZONE 'UTC')::date)
              AND (d.metros > {distancia} OR (d.metros = {distancia} AND p."Id" > {ultimoId}))
            ORDER BY d.metros, p."Id"
            LIMIT {limite}
            """).ToListAsync(ct);
    }
}
