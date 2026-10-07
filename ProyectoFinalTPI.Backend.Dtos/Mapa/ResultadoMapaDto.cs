using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Dtos.Mapa
{
    public class ResultadoMapaDto
    {
        public List<PublicacionPinDto> Pines { get; set; } = new();
        public bool HayMas { get; set; }
    }
}
