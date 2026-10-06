using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces
{
    public interface IPublicacionRepositorio
    {
        List<Publicacion> ObtenerTodasPublicaciones();
        void GuardarPublicacion(Publicacion publicacion);
        List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria);
    }
}
