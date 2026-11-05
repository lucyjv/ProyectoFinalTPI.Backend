namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IAdminComentarioServicio
{
    Task Eliminar(int id, CancellationToken cancellationToken = default);
}
