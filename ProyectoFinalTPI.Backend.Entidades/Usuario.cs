namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FotoPerfilUrl { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Contraseña { get; set; } = string.Empty;
        // Only rows present when the Google migration runs are eligible for automatic linking.
        public bool PuedeVincularGoogle { get; set; }
        public bool SoloGoogle { get; set; }
        public string? GoogleSubject { get; set; }
        public bool EsAdmin { get; set; }
        public bool EstaEliminado { get; set; }
        public DateTime? FechaEliminacion { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    }
}
