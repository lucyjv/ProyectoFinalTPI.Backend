namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IPortadaCapsulaAlmacenamiento
{
    Task<string> Subir(Stream archivo, CancellationToken ct);
    Task<byte[]> Leer(string recursoId, CancellationToken ct);
    Task Eliminar(string recursoId, CancellationToken ct);
}
