namespace ProyectoFinalTPI.Backend.Services
{
    public interface ISeguimientoService
    {
        Task<ResultadoSeguirUsuario> SeguirUsuarioAsync(
            int idUsuario,
            int idUsuarioASeguir,
            CancellationToken cancellationToken = default);
    }

    public enum ResultadoSeguirUsuario
    {
        Seguido,
        YaLoSeguía,
        AutoSeguimiento,
        UsuarioNoEncontrado
    }
}
