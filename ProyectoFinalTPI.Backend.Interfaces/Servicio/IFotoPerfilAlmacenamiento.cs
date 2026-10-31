namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IFotoPerfilAlmacenamiento
{
    Task<string> SubirAsync(Stream archivo, string nombreArchivo, CancellationToken cancellationToken = default);
}
