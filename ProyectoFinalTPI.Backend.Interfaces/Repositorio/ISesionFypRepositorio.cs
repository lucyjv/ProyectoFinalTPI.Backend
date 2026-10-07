using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio
{
    public interface ISesionFypRepositorio
    {
        SesionFypDto Crear(
            int usuarioId,
            IReadOnlyCollection<int> publicacionesIds);

        SesionFypDto? Obtener(
            Guid sesionId,
            int usuarioId);
    }
}
