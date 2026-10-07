using ProyectoFinalTPI.Backend.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IInteresServicio
    {
        Task<IReadOnlyList<InteresCategoriaDto>> ObtenerIntereses(int usuarioId, CancellationToken cancellationToken = default);
    }
}
