using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    /// <summary>
    /// Lógica de negocio de usuarios.
    /// </summary>
    public class UsuarioServicio : IUsuarioServicio
    {
        private readonly IUsuarioRepositorio _repositorio;
        private readonly ISeguimientoServicio? _seguimientoServicio;
        private readonly ILogroServicio _logroServicio;
        private readonly TimeProvider _timeProvider;

        public UsuarioServicio(
            IUsuarioRepositorio repositorio,
            ILogroServicio logroServicio,
            ISeguimientoServicio? seguimientoServicio = null, TimeProvider? timeProvider = null)
        {
            _repositorio = repositorio;
            _seguimientoServicio = seguimientoServicio;
            _logroServicio = logroServicio;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <inheritdoc/>
        public async Task<UsuarioResumenDto> ObtenerPorUsernameAsync(
            string username, CancellationToken cancellationToken = default)
        {
            var usuario = await _repositorio.ObtenerPorUsernameAsync(username, cancellationToken);

            if (usuario is null)
                throw new KeyNotFoundException($"No existe ningún usuario con el username '{username}'.");

            return new UsuarioResumenDto
            {
                UsuarioId = usuario.Id,
                Username  = usuario.Username
            };
        }

        /// <inheritdoc/>
        public async Task<UsuarioPerfilDto> ObtenerPerfilPorUsernameAsync(
            string username, int? usuarioAutenticadoId, CancellationToken cancellationToken = default)
        {
            var usuario = await _repositorio.ObtenerPorUsernameAsync(username, cancellationToken);

            if (usuario is null)
                throw new KeyNotFoundException($"No existe ningún usuario con el username '{username}'.");

            return await ConstruirPerfilDto(usuario, usuarioAutenticadoId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<UsuarioPerfilDto> ObtenerPerfilPorIdAsync(
            int usuarioId, int? usuarioAutenticadoId, CancellationToken cancellationToken = default)
        {
            var usuario = await _repositorio.ObtenerPorIdAsync(usuarioId);

            if (usuario is null)
                throw new KeyNotFoundException($"No existe ningún usuario con el identificador '{usuarioId}'.");

            return await ConstruirPerfilDto(usuario, usuarioAutenticadoId, cancellationToken);
        }

        public async Task<List<LogroEstadoDto>> RegistrarIngresoDiarioAsync(int usuarioId, CancellationToken cancellationToken = default)
        {
            var usuario = await _repositorio.ObtenerYUsuarioInteractivoPorIdAsync(usuarioId);
            if (usuario == null) return new();

            var ahora = _timeProvider.GetUtcNow().UtcDateTime;
            var hoy = ahora.Date;

            if (usuario.UltimoIngresoUtc.HasValue)
            {
                var ultimoIngreso = usuario.UltimoIngresoUtc.Value.Date;

                if (ultimoIngreso == hoy)
                {
                    return await ValidarLogros(usuarioId, usuario);
                }
                else if (ultimoIngreso == hoy.AddDays(-1))
                {
                    usuario.RachaDiasActual++;
                }
                else
                {
                    usuario.RachaDiasActual = 1;
                }
            }
            else
            {
                usuario.RachaDiasActual = 1;
            }

            usuario.UltimoIngresoUtc = ahora;
            
            await _repositorio.ActualizarAsync(usuario, cancellationToken);

            return await ValidarLogros(usuarioId, usuario);
        }

        private async Task<List<LogroEstadoDto>> ValidarLogros(int usuarioId, Usuario_Interactivo usuario)
        {
            var nuevos = new List<int>();
            if (usuario.RachaDiasActual >= 3 && await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.RachaDeHierro))
                nuevos.Add((int)LogroEnum.RachaDeHierro);

            if (usuario.RachaDiasActual >= 7 && await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.RachaDeOro))
                nuevos.Add((int)LogroEnum.RachaDeOro);
            return nuevos.Count == 0 ? new() : (await _logroServicio.ObtenerCatalogoParaUsuario(usuarioId))
                .Where(logro => nuevos.Contains(logro.Id)).ToList();
        }

        private async Task<UsuarioPerfilDto> ConstruirPerfilDto(
            Usuario usuario, int? usuarioAutenticadoId, CancellationToken cancellationToken)
        {
            var cantidadPublicaciones = await _repositorio.ContarPublicacionesVisiblesAsync(usuario.Id, cancellationToken);

            int cantidadSeguidores = 0;
            int cantidadSeguidos = 0;
            bool loSigo = false;

            if (_seguimientoServicio != null)
            {
                var seguidores = await _seguimientoServicio.ListarSeguidores(usuario.Id, cancellationToken);
                var seguidos = await _seguimientoServicio.ListarSeguidos(usuario.Id, cancellationToken);

                cantidadSeguidores = seguidores?.Count ?? 0;
                cantidadSeguidos = seguidos?.Count ?? 0;

                if (usuarioAutenticadoId.HasValue && usuarioAutenticadoId.Value > 0 && usuarioAutenticadoId.Value != usuario.Id)
                {
                    var seguidosAutenticado = await _seguimientoServicio.ObtenerSeguidosIdsAsync(usuarioAutenticadoId.Value, cancellationToken);
                    loSigo = seguidosAutenticado.Contains(usuario.Id);
                }
            }

            var dto = new UsuarioPerfilDto
            {
                Id = usuario.Id,
                Username = usuario.Username,
                CantidadPublicaciones = cantidadPublicaciones,
                CantidadSeguidores = cantidadSeguidores,
                CantidadSeguidos = cantidadSeguidos,
                LoSigo = loSigo,
                Logros = await _logroServicio.ObtenerCatalogoParaUsuario(usuario.Id)
            };

            if (usuario is Usuario_Personal personal)
            {
                dto.TipoUsuario = "Usuario_Personal";
                dto.Nombre = personal.Nombre;
                dto.Apellido = personal.Apellido;
                dto.Reputacion = FormatearReputacion(personal.Reputacion);
                dto.PuntosNostalgia = personal.PuntosNostalgia;
            }
            else if (usuario is Usuario_Marca marca)
            {
                dto.TipoUsuario = "Usuario_Marca";
                dto.Nombre = marca.NombreEmpresa;
                dto.Reputacion = "Marca";
                dto.PuntosNostalgia = marca.PuntosNostalgia;
            }
            else
            {
                dto.TipoUsuario = "Usuario";
                dto.Reputacion = "Errante";
            }

            return dto;
        }

        private static string FormatearReputacion(ReputacionEnum reputacion) => reputacion switch
        {
            ReputacionEnum.Errante => "Errante",
            ReputacionEnum.Nostalgico => "Nostálgico",
            ReputacionEnum.Aedo => "Aedo",
            ReputacionEnum.Mnnemónide => "Mnemosine",
            _ => "Errante"
        };
    }
}
