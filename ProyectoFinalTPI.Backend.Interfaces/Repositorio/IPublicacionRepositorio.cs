using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio
{
    public interface IPublicacionRepositorio
    {
        List<Publicacion> ObtenerTodasPublicaciones();
        void GuardarPublicacion(Publicacion publicacion);
        List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria);

        Task<int> Crear(Publicacion publicacion);
    }
}
