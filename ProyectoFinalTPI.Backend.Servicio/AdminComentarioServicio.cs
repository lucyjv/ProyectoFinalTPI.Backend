using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.Extensions;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AdminComentarioServicio(
    IAdminComentarioRepositorio repositorio,
    INotificacionServicio notificaciones) : IAdminComentarioServicio
{
    public async Task Eliminar(int id, CancellationToken cancellationToken = default)
    {
        id.ValidarId("comentario");
        var comentario = await repositorio.Obtener(id, cancellationToken)
            ?? throw new KeyNotFoundException("El comentario no existe.");
        if (comentario.EstaEliminado) return;
        await repositorio.Eliminar(comentario, cancellationToken);
        await notificaciones.NotificarAdministrativaAsync(
            comentario.UsuarioId, comentario.PublicacionId, cancellationToken);
    }
}
