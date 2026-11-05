using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class AdminPublicacionRepositorio(ApplicationDbContext context, IDriver? neo4jDriver = null)
    : IAdminPublicacionRepositorio
{
    public async Task<PaginaAdminDto<PublicacionAdminDto>> Listar(
        FiltroPublicacionesAdminDto filtro, CancellationToken cancellationToken = default)
    {
        var consulta = AplicarFiltros(context.Publicaciones.AsNoTracking(), filtro);
        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta.OrderByDescending(p => p.FechaCreación).ThenByDescending(p => p.Id)
            .Skip((filtro.Pagina - 1) * filtro.Tamanio).Take(filtro.Tamanio)
            .Select(p => new PublicacionAdminDto
            {
                Id = p.Id, Titulo = p.Titulo, Descripcion = p.Descripcion,
                Fecha = p.Fecha, FechaCreacion = p.FechaCreación, Categoria = p.Categoria,
                EstaOculto = p.EstaOculto, MotivoOculto = p.MotivoOculto,
                AutorId = p.UsuarioId, AutorUsername = p.Usuario.Username
            }).ToListAsync(cancellationToken);
        return new() { Items = items, Pagina = filtro.Pagina, Tamanio = filtro.Tamanio, TotalItems = total };
    }

    public Task<Publicacion?> Obtener(int id, CancellationToken cancellationToken = default) =>
        context.Publicaciones.Include(p => p.Usuario).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task Guardar(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);

    public async Task Eliminar(Publicacion publicacion, CancellationToken cancellationToken = default)
    {
        var idsEliminados = await ObtenerIdsEnCascada(publicacion.Id, cancellationToken);
        context.Publicaciones.Remove(publicacion);
        await context.SaveChangesAsync(cancellationToken);
        if (neo4jDriver is null) return;
        await using var sesion = neo4jDriver.AsyncSession();
        await sesion.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Publicacion) WHERE p.postgresId IN $ids DETACH DELETE p", new { ids = idsEliminados });
            await cursor.ConsumeAsync();
        });
    }

    private async Task<int[]> ObtenerIdsEnCascada(int publicacionId, CancellationToken cancellationToken)
    {
        var encontrados = new HashSet<int> { publicacionId };
        var pendientes = new[] { publicacionId };
        while (pendientes.Length > 0)
        {
            var siguientes = await context.Publicaciones.AsNoTracking()
                .Where(p => p.PublicacionOriginalId.HasValue && pendientes.Contains(p.PublicacionOriginalId.Value))
                .Select(p => p.Id).ToArrayAsync(cancellationToken);
            pendientes = siguientes.Where(encontrados.Add).ToArray();
        }
        return encontrados.ToArray();
    }

    private static IQueryable<Publicacion> AplicarFiltros(
        IQueryable<Publicacion> consulta, FiltroPublicacionesAdminDto filtro)
    {
        if (filtro.Categoria.HasValue) consulta = consulta.Where(p => p.Categoria == filtro.Categoria);
        if (filtro.AutorId.HasValue) consulta = consulta.Where(p => p.UsuarioId == filtro.AutorId);
        if (filtro.Oculta.HasValue) consulta = consulta.Where(p => p.EstaOculto == filtro.Oculta);
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim().ToLower();
            consulta = consulta.Where(p => p.Titulo.ToLower().Contains(texto) ||
                p.Descripcion.ToLower().Contains(texto) || p.Usuario.Username.ToLower().Contains(texto));
        }
        return consulta;
    }
}
