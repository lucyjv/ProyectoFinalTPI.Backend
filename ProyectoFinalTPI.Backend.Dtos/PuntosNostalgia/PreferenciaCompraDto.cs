namespace ProyectoFinalTPI.Backend.Dtos.PuntosNostalgia;

public sealed record PreferenciaCompraDto(
    int CompraId,
    string PreferenceId,
    string InitPoint,
    string PackId,
    string PackNombre,
    int Puntos,
    decimal PrecioArs,
    decimal PrecioUsd,
    decimal CotizacionDolar);
