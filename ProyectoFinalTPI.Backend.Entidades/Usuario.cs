namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Contraseña { get; set; } = string.Empty;
        public bool EsAdmin { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public ICollection<Publicacion> Publicaciones { get; set; }
            = new List<Publicacion>();

    }
}
