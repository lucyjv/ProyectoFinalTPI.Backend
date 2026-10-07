using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IRecomendacionServicio
    {
        Task<IReadOnlyList<int>> GenerarRecomendacionParaUsuario(int usuarioId, int cantidadMaxima, CancellationToken cancellationToken = default);
    }
}
