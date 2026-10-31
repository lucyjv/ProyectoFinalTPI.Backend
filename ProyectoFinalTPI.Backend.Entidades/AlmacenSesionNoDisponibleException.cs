namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class AlmacenSesionNoDisponibleException(Exception innerException)
    : Exception("El feed está temporalmente no disponible. Intentá nuevamente en unos instantes.", innerException);
