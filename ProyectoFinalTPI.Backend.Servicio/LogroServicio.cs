using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class LogroServicio : ILogroServicio
    {
        private readonly ILogrosUsuariosRepositorio _logrosUsuariosRepositorio;

        public LogroServicio(ILogrosUsuariosRepositorio logrosUsuariosRepositorio)
        {
            _logrosUsuariosRepositorio = logrosUsuariosRepositorio;
        }

        #region Operaciones públicas

        public async Task<bool> VerificarYDarLogro(int idUsuario, LogroEnum logro)
        {
            int idLogro = (int)logro;
            if (await _logrosUsuariosRepositorio.ExisteRegistro(idUsuario, idLogro)) return false;
            return await _logrosUsuariosRepositorio.Guardar(CrearLogroDeUsuario(idUsuario, idLogro));
        }
        public async Task<List<Logros_Usuarios>> MostrarLogrosDeUsuario(int idUsuario)
        {
            return await _logrosUsuariosRepositorio.ObtenerPorUsuario(idUsuario);
        }

        public async Task<List<LogroEstadoDto>> ObtenerCatalogoParaUsuario(int idUsuario)
        {
            var catalogo = await _logrosUsuariosRepositorio.ObtenerCatalogo();
            Dictionary<int, DateTime> obtenidos = await ObtenerFechaDeDesbloqueo(idUsuario);

            return catalogo.Select(logro => MapearLogroEstado(logro, obtenidos)).ToList();
        }

        #endregion

        #region Métodos privados y validaciones

        private Logros_Usuarios CrearLogroDeUsuario(int idUsuario, int idLogro)
        {
            var LogroUsuario = new Logros_Usuarios
            {
                UsuarioId = idUsuario,
                LogroId = idLogro,
                FechaDesbloqueo = DateTime.UtcNow
            };
            
            return LogroUsuario;
        }
        private static LogroEstadoDto MapearLogroEstado(Logro logro, Dictionary<int, DateTime> obtenidos)
        {
            var desbloqueado = obtenidos.TryGetValue(logro.Id, out var fecha);
            return new LogroEstadoDto
            {
                Id = logro.Id,
                Titulo = logro.Titulo,
                Descripcion = logro.Descripcion,
                UrlIcono = logro.UrlIcono,
                PuntosRecompensa = logro.PuntosRecompensa,
                Desbloqueado = desbloqueado,
                FechaDesbloqueo = desbloqueado ? fecha : null
            };
        }

        private async Task<Dictionary<int, DateTime>> ObtenerFechaDeDesbloqueo(int idUsuario)
        {
            return (await _logrosUsuariosRepositorio.ObtenerPorUsuario(idUsuario))
                .ToDictionary(registro => registro.LogroId, registro => registro.FechaDesbloqueo);
        }

        #endregion
    }
}
