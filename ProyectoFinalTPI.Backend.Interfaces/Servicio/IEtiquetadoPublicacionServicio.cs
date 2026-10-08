namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface IEtiquetadoPublicacionServicio
    {
        Task<int> EtiquetarUsuarios(
            int publicacionId,
            IReadOnlyCollection<int> usuariosIds,
            CancellationToken cancellationToken = default);
    }
}
