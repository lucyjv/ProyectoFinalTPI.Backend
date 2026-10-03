using NetTopologySuite.Geometries;

namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Lugar
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public Point Coordenadas { get; set; } = Point.Empty;
    }
}
