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
        Task<SesionFypDto> CrearAsync(
            int usuarioId,
            IReadOnlyCollection<int> publicacionesIds,
            CancellationToken cancellationToken = default);

        Task<SesionFypDto?> ObtenerAsync(
            Guid sesionId,
            int usuarioId,
            CancellationToken cancellationToken = default);
    }
}
