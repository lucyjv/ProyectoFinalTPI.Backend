using ProyectoFinalTPI.Backend.Entidades;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Dtos
{
    public class InteresCategoriaDto
    {
        public CategoriaEnum Categoria { get; set; }
        public double Afinidad { get; set; }
    }
}
