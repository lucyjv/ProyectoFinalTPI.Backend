using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AdminComentarioServicio(
    IAdminComentarioRepositorio repositorio,
    INotificacionServicio notificaciones) : IAdminComentarioServicio
{
    public async Task Eliminar(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ArgumentException("El ID de comentario debe ser mayor que cero.");
        var comentario = await repositorio.Obtener(id, cancellationToken)
            ?? throw new KeyNotFoundException("El comentario no existe.");
        if (comentario.EstaEliminado) return;
        await repositorio.Eliminar(comentario, cancellationToken);
        await notificaciones.NotificarAdministrativaAsync(
            comentario.UsuarioId, comentario.PublicacionId, cancellationToken);
    }
}
