using System.ComponentModel.DataAnnotations.Schema;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;

namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Publicacion
    {
            public int Id { get; set; }
            public string Titulo { get; set; } = string.Empty;
            public string Descripcion { get; set; } = string.Empty;
            public DateTime Fecha { get; set; }

            [NotMapped]
            public Año AñoDelRecuerdo => Año.DesdeFecha(Fecha);

            [NotMapped]
            public Decada DecadaDelRecuerdo => AñoDelRecuerdo.ADecada();
            public string UrlMultimedia { get; set; } = string.Empty;
            public MultimediaEnum TipoMultimedia { get; set; }
            public bool EstaOculto { get; set; }
            public string? MotivoOculto { get; set; }
            public DateTime FechaCreación { get; set; } = DateTime.UtcNow;
            public int LugarId { get; set; }
            public Lugar Lugar { get; set; } = null!;
            public CategoriaEnum Categoria { get; set; }
            public int UsuarioId { get; set; }    
            public Usuario_Interactivo Usuario { get; set; } = null!;
            public ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();

            public int? PublicacionOriginalId { get; set; }
            public Publicacion? PublicacionOriginal { get; set; }
    }

    public enum CategoriaEnum
    { 
        Lugares,
        Música,
        Cine,
        Televisión,
        Videojuegos,
        Acontecimientos,
        Recuerdos_Personales
    }

    public enum MultimediaEnum
    {
        Foto,
        Video
    }
}
