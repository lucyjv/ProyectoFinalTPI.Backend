namespace ProyectoFinalTPI.Backend.Dtos.Usuario
{
    public class UsuarioPerfilDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FotoPerfilUrl { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public string TipoUsuario { get; set; } = string.Empty;
        public string Reputacion { get; set; } = "Errante";
        public int PuntosNostalgia { get; set; }
        public int CantidadPublicaciones { get; set; }
        public int CantidadSeguidores { get; set; }
        public int CantidadSeguidos { get; set; }
        public bool LoSigo { get; set; }
        public List<LogroEstadoDto> Logros { get; set; } = new();
    }
}
