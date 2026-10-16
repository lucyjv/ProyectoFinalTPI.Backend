using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql.PostgresTypes;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class LogroServicio : ILogroServicio
    {
        private readonly IPublicacionRepositorio _publicacionRepositorio;
        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly ILogrosUsuariosRepositorio _logrosUsuariosRepositorio;

        public LogroServicio(IPublicacionRepositorio publicacionRepositorio, IUsuarioRepositorio usuarioRepositorio, ILogrosUsuariosRepositorio logrosUsuariosRepositorio)
        {
            _publicacionRepositorio = publicacionRepositorio;
            _usuarioRepositorio = usuarioRepositorio;
            _logrosUsuariosRepositorio = logrosUsuariosRepositorio;
        }

        public void EvaluarLogro(int idUsuario, int idLogro)
        {
            bool ya_logrado = _logrosUsuariosRepositorio.ExisteRegistro(idUsuario, idLogro);
            if (ya_logrado)
            {
                return;
            }
            var nuevoLogroDesbloqueado = CrearLogroDeUsuario(idUsuario, idLogro, DateTime.UtcNow);

            _logrosUsuariosRepositorio.Guardar(nuevoLogroDesbloqueado);
        }
        public void MostrarLogrosDeUsuario(int idUsuario)
        {
            _logrosUsuariosRepositorio.ObtenerPorUsuario(idUsuario);
        }

        private Logros_Usuarios CrearLogroDeUsuario(int idUsuario, int idLogro, DateTime fechaDesbloqueo)
        {
            var LogroUsuario = new Logros_Usuarios
            {
                UsuarioId = idUsuario,
                LogroId = idLogro,
                FechaDesbloqueo = fechaDesbloqueo
            };
            
            return LogroUsuario;
        }
    }
}
