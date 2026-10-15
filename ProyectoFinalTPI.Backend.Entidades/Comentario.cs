namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Comentario
    {
        public int Id { get; set; }
        public string Contenido { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public bool EstaOculto { get; set; }
        public int PublicacionId { get; set; }
        public Publicacion Publicacion { get; set; } = null!;
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;
        public int? ComentarioPadreId { get; set; }
        public Comentario? ComentarioPadre { get; set; }
        public ICollection<Comentario> Respuestas { get; set; } = new List<Comentario>();
        public ICollection<ReaccionComentario> Reacciones { get; set; } = new List<ReaccionComentario>();
    }
}
