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

        public void EvaluarLogroUnaPublicacion(int idUsuario)
        {
            bool ya_logrado = _logrosUsuariosRepositorio.ExisteRegistro(idUsuario, 1);
            if (ya_logrado)
            {
                return;
            }
            var nuevoLogroDesbloqueado = CrearLogroUsuario(idUsuario, 2, DateTime.UtcNow);

            _logrosUsuariosRepositorio.Guardar(nuevoLogroDesbloqueado);
        }

        private Logros_Usuarios CrearLogroUsuario(int idUsuario, int idLogro, DateTime fechaDesbloqueo)
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
