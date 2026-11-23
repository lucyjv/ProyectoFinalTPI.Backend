namespace ProyectoFinalTPI.Backend.Dtos.PuntosNostalgia;

public sealed record PackPuntosDto(
    string Id,
    string Nombre,
    int Puntos,
    decimal PrecioUsd,
    decimal PrecioArs,
    string Icono,
    string? Etiqueta = null,
    bool Destacado = false);
