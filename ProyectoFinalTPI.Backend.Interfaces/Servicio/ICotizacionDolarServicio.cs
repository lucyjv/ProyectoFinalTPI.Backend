namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface ICotizacionDolarServicio
{
    Task<decimal> ObtenerCotizacionAsync(CancellationToken ct = default);
}
