namespace ProyectoFinalTPI.Backend.Entidades
{
    public abstract class Usuario_Interactivo : Usuario
    {
        public int PuntosNostalgia { get; set; }
        public DateTime? SuspendidoHasta { get; set; }
        public ICollection<Publicacion> Publicaciones { get; set; } = new List<Publicacion>();
        public ICollection<Logros_Usuarios> LogrosUsuarios { get; set; } = new List<Logros_Usuarios>()
        public DateTime? UltimoIngresoUtc { get; set; }
        public int RachaDiasActual { get; set; } = 0;
    }
}
