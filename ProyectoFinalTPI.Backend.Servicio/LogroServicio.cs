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
using ProyectoFinalTPI.Backend.Dtos.Usuario;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class LogroServicio : ILogroServicio
    {
        private readonly ILogrosUsuariosRepositorio _logrosUsuariosRepositorio;

        public LogroServicio(ILogrosUsuariosRepositorio logrosUsuariosRepositorio)
        {
            _logrosUsuariosRepositorio = logrosUsuariosRepositorio;
        }

        public async Task VerificarYDarLogro(int idUsuario, LogroEnum logro)
        {
            int idLogro = (int)logro;
            bool ya_logrado = await _logrosUsuariosRepositorio.ExisteRegistro(idUsuario, idLogro);
            if (ya_logrado)
            {
                return;
            }
            var nuevoLogroDesbloqueado = CrearLogroDeUsuario(idUsuario, idLogro, DateTime.UtcNow);

            await _logrosUsuariosRepositorio.Guardar(nuevoLogroDesbloqueado);
        }
        public async Task<List<Logros_Usuarios>> MostrarLogrosDeUsuario(int idUsuario)
        {
            return await _logrosUsuariosRepositorio.ObtenerPorUsuario(idUsuario);
        }

        public async Task<List<LogroEstadoDto>> ObtenerCatalogoParaUsuario(int idUsuario)
        {
            var catalogo = await _logrosUsuariosRepositorio.ObtenerCatalogo();
            var obtenidos = (await _logrosUsuariosRepositorio.ObtenerPorUsuario(idUsuario))
                .ToDictionary(registro => registro.LogroId, registro => registro.FechaDesbloqueo);

            return catalogo.Select(logro => new LogroEstadoDto
            {
                Id = logro.Id,
                Titulo = logro.Titulo,
                Descripcion = logro.Descripcion,
                UrlIcono = logro.UrlIcono,
                PuntosRecompensa = logro.PuntosRecompensa,
                Desbloqueado = obtenidos.ContainsKey(logro.Id),
                FechaDesbloqueo = obtenidos.TryGetValue(logro.Id, out var fecha) ? fecha : null
            }).ToList();
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
