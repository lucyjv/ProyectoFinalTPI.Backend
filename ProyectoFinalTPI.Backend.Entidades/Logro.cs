using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Logro
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string UrlIcono { get; set; } = string.Empty;
        public int PuntosRecompensa { get; set; }
        public ICollection<Logros_Usuarios> LogrosUsuarios { get; set; } = new List<Logros_Usuarios>();

    }
}
