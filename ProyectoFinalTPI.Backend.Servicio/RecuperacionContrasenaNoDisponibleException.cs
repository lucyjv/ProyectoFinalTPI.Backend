namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class RecuperacionContrasenaNoDisponibleException : Exception
{
    public RecuperacionContrasenaNoDisponibleException()
        : base("La recuperación de contraseña no está disponible en este momento. Intentá más tarde.") { }
}
