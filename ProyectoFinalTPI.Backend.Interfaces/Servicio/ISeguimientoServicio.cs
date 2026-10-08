using ProyectoFinalTPI.Backend.Dtos.Usuario;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface ISeguimientoServicio
    {
        Task<ResultadoSeguimiento> SeguirUsuario(
            int idUsuario,
            int idUsuarioASeguir,
            CancellationToken cancellationToken = default);

        Task<ResultadoSeguimiento> DejarDeSeguirUsuario(
            int idUsuario,
            int idUsuarioASeguir,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AutorResumenDto>?> ListarSeguidos(
            int idUsuario,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AutorResumenDto>?> ListarSeguidores(
            int idUsuario, CancellationToken cancellationToken = default);
    }

    public enum ResultadoSeguimiento
    {
        Aplicado,
        SinCambios, // ya seguia al usuario o ya habia dejado de seguirloi
        AutoSeguimiento,
        UsuarioNoEncontrado
    }
}
