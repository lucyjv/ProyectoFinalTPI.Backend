using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Dtos.Lugar
{
    public class LugarDetalleDto
    {
        public int Id {  get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public double Latitud {  get; set; }
        public double Longitud { get; set; }
    }
}
