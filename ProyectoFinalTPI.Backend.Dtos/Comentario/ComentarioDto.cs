using ProyectoFinalTPI.Backend.Dtos.Usuario;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Dtos.Comentario
{
    public class ComentarioDto
    {
        public int Id { get; set; }
        public int PublicacionId { get; set; }
        public int? ComentarioPadreId { get; set; }
        public string? Contenido { get; set; }
        public DateTime FechaCreacion { get; set; }
        public AutorResumenDto? Autor { get; set; }
        public bool EstaEliminado { get; set; }
        public bool PuedeEliminar { get; set; }
        public int CantidadRespuestas { get; set; }
        public string? SiguienteCursorRespuestas { get; set; }
        public Dictionary<string, int> Reacciones { get; set; } = new();
        public string? ReaccionActual { get; set; }
        public List<ComentarioDto> Respuestas { get; set; } = new();
    }
}
