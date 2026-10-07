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
        public string Contenido { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }

        public AutorResumenDto Autor { get; set; } = null!;
    }
}
