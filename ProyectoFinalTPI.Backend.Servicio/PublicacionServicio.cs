using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class PublicacionServicio : IPublicacionServicio
    {
        private readonly IPublicacionRepositorio _publicacionRepositorio;
        public PublicacionServicio(IPublicacionRepositorio publicacionRepositorio)
        {
            _publicacionRepositorio = publicacionRepositorio;
        }

        public List<Publicacion> ObtenerPublicaciones()
        {
            return _publicacionRepositorio.ObtenerTodasPublicaciones();
        }
        public void AgregarPublicacion(Publicacion publicacion)
        {
            publicacion.EstaOculto = false;
            _publicacionRepositorio.GuardarPublicacion(publicacion);
        }
        public List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria)
        {
            return _publicacionRepositorio.ObtenerPublicacionPorCategoria(categoria);
        }

    }
}
