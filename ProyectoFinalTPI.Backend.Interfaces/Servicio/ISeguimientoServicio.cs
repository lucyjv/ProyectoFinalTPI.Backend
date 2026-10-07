namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface ISeguimientoServicio
    {
        Task<ResultadoSeguirUsuario> SeguirUsuarioAsync(
            int idUsuario,
            int idUsuarioASeguir,
            CancellationToken cancellationToken = default);

        Task<ResultadoDejarDeSeguirUsuario> DejarDeSeguirUsuarioAsync(
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

    public enum ResultadoDejarDeSeguirUsuario
    {
        DejadoDeSeguir,
        YaNoLoSeguía,
        AutoSeguimiento,
        UsuarioNoEncontrado
    }
}
