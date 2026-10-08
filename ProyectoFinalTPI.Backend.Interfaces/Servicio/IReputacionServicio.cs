using System.Threading;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IReputacionServicio
    {
        Task EvaluarRangoUsuarioAsync(int usuarioId, CancellationToken cancellationToken = default);
    }
}