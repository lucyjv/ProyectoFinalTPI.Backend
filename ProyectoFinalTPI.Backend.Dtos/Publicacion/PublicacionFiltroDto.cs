using ProyectoFinalTPI.Backend.Entidades;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion
{
    public class PublicacionFiltroDto
    {
        public double MinLatitud { get; set; }
        public double MaxLatitud { get; set; }
        public double MinLongitud { get; set; }
        public double MaxLongitud { get; set; }

        public CategoriaEnum? Categoria { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }
}
