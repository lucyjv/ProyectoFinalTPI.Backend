using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Dtos
{
    public class ResultadoPaginadoDto<T>
    {
        public List<T> Items { get; set; } = new();

        public string? SiguienteCursor { get; set; }

        public bool HayMas => SiguienteCursor != null;
    }
}
