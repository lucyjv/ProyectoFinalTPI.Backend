using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos
{
    public class CrearPublicacionDto
    {
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }

        public CategoriaEnum Categoria { get; set; }

        public string NombreLugar { get; set; } = string.Empty;

        public double Latitud { get; set; }
        public double Longitud { get; set; }

        public MultimediaEnum TipoMultimedia { get; set; }

        public Stream? Archivo { get; set; }
        public string? NombreArchivo { get; set; }
        public string? UrlArchivo { get; set; }

        public int AutorId { get; set; }
    }
}