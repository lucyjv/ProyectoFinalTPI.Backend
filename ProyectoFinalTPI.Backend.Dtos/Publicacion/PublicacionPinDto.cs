using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion
{
    public class PublicacionPinDto
    {
        public int Id { get; set; }
        public double Latitud {  get; set; }
        public double Longitud { get; set; }
        public CategoriaEnum Categoria { get; set; }

    }
}
