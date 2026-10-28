namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class OpcionesPublicacion
{
    public const string Seccion = "Publicaciones";

    // Temporalmente opcional para la presentación. Activar esta opción para
    // volver a exigir foto/video sin quitar las validaciones de los adjuntos.
    public bool RequerirMultimedia { get; set; } = false;
}
