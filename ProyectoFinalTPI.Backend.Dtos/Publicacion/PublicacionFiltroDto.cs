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
        /// <summary>Latitud mínima del área, entre -90 y 90. Debe ser menor que MaxLatitud.</summary>
        public double MinLatitud { get; set; }
        /// <summary>Latitud máxima del área, entre -90 y 90.</summary>
        public double MaxLatitud { get; set; }
        /// <summary>Longitud mínima del área, entre -180 y 180. Debe ser menor que MaxLongitud.</summary>
        public double MinLongitud { get; set; }
        /// <summary>Longitud máxima del área, entre -180 y 180.</summary>
        public double MaxLongitud { get; set; }

        /// <summary>Categoría opcional. Si se omite, incluye todas las categorías.</summary>
        public CategoriaEnum? Categoria { get; set; }
        /// <summary>Fecha del recuerdo desde la que buscar, inclusive. Opcional y en UTC, por ejemplo 1990-01-01T00:00:00Z.</summary>
        public DateTime? FechaDesde { get; set; }
        /// <summary>Fecha del recuerdo hasta la que buscar, exclusiva. Opcional, en UTC y posterior a FechaDesde.</summary>
        public DateTime? FechaHasta { get; set; }
    }
}
