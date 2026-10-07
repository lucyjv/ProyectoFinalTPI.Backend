namespace ProyectoFinalTPI.Backend.Entidades
{
    public abstract class Usuario_Interactivo : Usuario
    {
        public int PuntosNostalgia { get; set; }
        public DateTime? SuspendidoHasta { get; set; }
        public ICollection<Publicacion> Publicaciones { get; set; } = new List<Publicacion>();

    }
}
