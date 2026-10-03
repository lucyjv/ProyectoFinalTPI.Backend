namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Publicacion
    {
            public int Id { get; set; }
            public string Titulo { get; set; } = string.Empty;
            public string Descripcion { get; set; } = string.Empty;
            public int Año { get; set; }
            public string Foto { get; set; } = string.Empty;
            public bool EstaOculto { get; set; }
            public string? MotivoOculto { get; set; }
            public DateTime FechaCreación { get; set; } = DateTime.UtcNow;
            public Lugar Lugar { get; set; } = null!;
            public CategoriaEnum Categoria { get; set; }
            public ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();

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
}
