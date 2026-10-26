using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class ComentarioRepositorio(ApplicationDbContext context) : IComentarioRepositorio
{
    private IQueryable<Comentario> Detalles() => context.Comentarios
        .AsNoTracking()
        .Include(comentario => comentario.Usuario)
        .Include(comentario => comentario.Reacciones);

    private static IQueryable<Comentario> AplicarCursor(
        IQueryable<Comentario> consulta, CursorCronologicoDto? cursor)
    {
        if (cursor is null) return consulta;
        return consulta.Where(comentario =>
            comentario.FechaCreacion < cursor.FechaCreacion ||
            comentario.FechaCreacion == cursor.FechaCreacion && comentario.Id < cursor.Id);
    }

    private static IQueryable<Comentario> Visibles(IQueryable<Comentario> consulta) =>
        consulta.Where(comentario => !comentario.EstaOculto &&
            (!comentario.EstaEliminado || comentario.Respuestas.Any(respuesta => !respuesta.EstaOculto)));

    public Task<bool> ExistePublicacionVisible(int publicacionId, CancellationToken cancellationToken = default) =>
        context.Publicaciones.AnyAsync(publicacion =>
            publicacion.Id == publicacionId && !publicacion.EstaOculto, cancellationToken);

    public Task<Comentario?> Obtener(int publicacionId, int comentarioId, CancellationToken cancellationToken = default) =>
        context.Comentarios.FirstOrDefaultAsync(comentario =>
            comentario.Id == comentarioId && comentario.PublicacionId == publicacionId && !comentario.EstaOculto,
            cancellationToken);

    public Task<Comentario?> ObtenerDetalle(
        int publicacionId, int comentarioId, CancellationToken cancellationToken = default) =>
        Detalles().FirstOrDefaultAsync(comentario =>
            comentario.Id == comentarioId && comentario.PublicacionId == publicacionId && !comentario.EstaOculto,
            cancellationToken);

    public Task<List<Comentario>> ObtenerRaices(
        int publicacionId, CursorCronologicoDto? cursor, int limite, CancellationToken cancellationToken = default)
    {
        var consulta = Visibles(Detalles().Where(comentario =>
            comentario.PublicacionId == publicacionId && comentario.ComentarioPadreId == null));
        return AplicarCursor(consulta, cursor)
            .OrderByDescending(comentario => comentario.FechaCreacion)
            .ThenByDescending(comentario => comentario.Id)
            .Take(limite + 1)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Comentario>> ObtenerRespuestas(
        int publicacionId, int comentarioPadreId, CursorCronologicoDto? cursor, int limite,
        CancellationToken cancellationToken = default)
    {
        var consulta = Visibles(Detalles().Where(comentario =>
            comentario.PublicacionId == publicacionId && comentario.ComentarioPadreId == comentarioPadreId));
        return AplicarCursor(consulta, cursor)
            .OrderByDescending(comentario => comentario.FechaCreacion)
            .ThenByDescending(comentario => comentario.Id)
            .Take(limite + 1)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<int, int>> ContarRespuestasDirectas(
        IReadOnlyCollection<int> comentariosIds, CancellationToken cancellationToken = default)
    {
        if (comentariosIds.Count == 0) return new Dictionary<int, int>();
        var respuestas = await context.Comentarios
            .AsNoTracking()
            .Where(respuesta => respuesta.ComentarioPadreId.HasValue &&
                comentariosIds.Contains(respuesta.ComentarioPadreId.Value) &&
                !respuesta.EstaOculto &&
                (!respuesta.EstaEliminado || respuesta.Respuestas.Any(nieta => !nieta.EstaOculto)))
            .Select(respuesta => respuesta.ComentarioPadreId!.Value)
            .ToListAsync(cancellationToken);
        return respuestas
            .GroupBy(comentarioPadreId => comentarioPadreId)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());
    }

    public async Task<int> Crear(Comentario comentario, CancellationToken cancellationToken = default)
    {
        await context.Comentarios.AddAsync(comentario, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return comentario.Id;
    }

    public async Task Eliminar(Comentario comentario, CancellationToken cancellationToken = default)
    {
        comentario.EstaEliminado = true;
        comentario.FechaEliminacion = DateTime.UtcNow;
        comentario.Contenido = string.Empty;
        var reacciones = await context.ReaccionesComentario
            .Where(reaccion => reaccion.ComentarioId == comentario.Id)
            .ToListAsync(cancellationToken);
        context.ReaccionesComentario.RemoveRange(reacciones);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task EstablecerReaccion(
        int comentarioId, int usuarioId, TipoReaccionComentario tipo,
        CancellationToken cancellationToken = default)
    {
        var reaccion = await context.ReaccionesComentario.FindAsync(
            new object[] { comentarioId, usuarioId }, cancellationToken);
        if (reaccion is null)
        {
            context.ReaccionesComentario.Add(new ReaccionComentario
            {
                ComentarioId = comentarioId,
                UsuarioId = usuarioId,
                Tipo = tipo,
                FechaCreacion = DateTime.UtcNow
            });
        }
        else
        {
            reaccion.Tipo = tipo;
            reaccion.FechaCreacion = DateTime.UtcNow;
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task QuitarReaccion(
        int comentarioId, int usuarioId, CancellationToken cancellationToken = default)
    {
        var reaccion = await context.ReaccionesComentario.FindAsync(
            new object[] { comentarioId, usuarioId }, cancellationToken);
        if (reaccion is null) return;
        context.ReaccionesComentario.Remove(reaccion);
        await context.SaveChangesAsync(cancellationToken);
    }
    public async Task<int> ContarComentariosEnPosteosAjenosAsync(int usuarioId,
            CancellationToken cancellationToken = default)
    {
        return await context.Comentarios
        .Where(c => c.UsuarioId == usuarioId && c.Publicacion.UsuarioId != usuarioId && !c.EstaOculto)
        .CountAsync(cancellationToken);
    }
    public async Task<int> ContarComentariosTotalesAsync(int usuarioId,
        CancellationToken cancellationToken = default)
    {
        return await context.Comentarios
        .Where(c => c.UsuarioId == usuarioId && !c.EstaOculto)
        .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Publicacion?> ObtenerPublicacionAsync(
    int publicacionId, CancellationToken cancellationToken = default)
    {
        return await context.Publicaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == publicacionId, cancellationToken);
    }
}
