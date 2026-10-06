using ProyectoFinalTPI.Backend.Entidades;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IMultimediaServicio
    {
        Task<string> SubirAsync(
            Stream? archivo,
            string? nombreArchivo,
            string? urlExterna,
            MultimediaEnum tipo);
    }
}
