using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces
{
    public interface IPublicacionServicio
    {
        List<Publicacion> ObtenerPublicaciones();
        void AgregarPublicacion(Publicacion publicacion);
        List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria);
    }
}
