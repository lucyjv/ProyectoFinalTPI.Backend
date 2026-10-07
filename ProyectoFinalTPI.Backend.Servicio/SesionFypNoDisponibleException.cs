using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class SesionFypNoDisponibleException : Exception
    {
        public SesionFypNoDisponibleException()
            : base(
                "La sesión del feed no está disponible. " +
                "Actualizá el feed para obtener nuevas recomendaciones.")
        {
        }
    }
}
