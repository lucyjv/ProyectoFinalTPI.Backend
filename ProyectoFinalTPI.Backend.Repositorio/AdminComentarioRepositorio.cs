using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class AdminComentarioRepositorio(ApplicationDbContext context) : IAdminComentarioRepositorio
{
    public Task<Comentario?> Obtener(int id, CancellationToken cancellationToken = default) =>
        context.Comentarios.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task Eliminar(Comentario comentario, CancellationToken cancellationToken = default)
    {
        comentario.EstaEliminado = true;
        comentario.EstaOculto = true;
        comentario.FechaEliminacion = DateTime.UtcNow;
        comentario.Contenido = string.Empty;
        var reacciones = await context.ReaccionesComentario.Where(r => r.ComentarioId == comentario.Id)
            .ToListAsync(cancellationToken);
        context.ReaccionesComentario.RemoveRange(reacciones);
        await context.SaveChangesAsync(cancellationToken);
    }
}
