using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Dtos
{
    public class CursorCronologicoDto
    {
        public DateTime FechaCreacion { get; set; }
        public int Id { get; set; }
        // Desempate para eventos de Seguidos. Cero representa la publicación original.
        public int ReposteadorId { get; set; }
    }
}
