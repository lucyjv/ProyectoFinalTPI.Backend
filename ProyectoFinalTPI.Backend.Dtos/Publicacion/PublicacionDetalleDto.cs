using ProyectoFinalTPI.Backend.Dtos.Lugar;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion
{
    public class PublicacionDetalleDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }
        public DateTime FechaCreacion { get; set; }
        public CategoriaEnum Categoria { get; set; }

        public string UrlMultimedia { get; set; } = string.Empty;
        public MultimediaEnum TipoMultimedia { get; set; }

        public AutorResumenDto Autor { get; set; } = null!;
        public LugarDetalleDto Lugar { get; set; } = null!;

        public int CantidadLikes { get; set; }
        public int CantidadComentarios { get; set; }
        public bool DioLike { get; set; }
    }
}
