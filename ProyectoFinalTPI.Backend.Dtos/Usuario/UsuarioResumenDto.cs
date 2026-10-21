namespace ProyectoFinalTPI.Backend.Dtos.Usuario
{
    /// <summary>
    /// Datos mínimos de un usuario devueltos por la búsqueda por username.
    /// </summary>
    public class UsuarioResumenDto
    {
        public int UsuarioId { get; set; }
        public string Username { get; set; } = string.Empty;
    }
}
