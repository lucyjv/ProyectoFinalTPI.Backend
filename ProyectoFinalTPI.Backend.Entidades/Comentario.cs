namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Comentario
    {
        public int Id { get; set; }
        public string Contenido { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public bool EstaOculto { get; set; }
        public Publicacion Publicacion { get; set; } = null!;
        public Usuario Usuario { get; set; } = null!;
    }
}
