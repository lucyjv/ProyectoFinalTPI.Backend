namespace ProyectoFinalTPI.Backend.Dtos.Comentario
{
    public class CrearComentarioDto
    {
        public string Contenido { get; set; } = string.Empty;
        public int UsuarioId { get; set; }
    }
}
