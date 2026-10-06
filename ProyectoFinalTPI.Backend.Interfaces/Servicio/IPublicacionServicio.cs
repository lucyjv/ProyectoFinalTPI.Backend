using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Dtos;
namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IPublicacionServicio
    {
        List<Publicacion> ObtenerPublicaciones();
        void AgregarPublicacion(Publicacion publicacion);
        List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria);

        Task<int> Crear(CrearPublicacionDto dto);
    }
}
