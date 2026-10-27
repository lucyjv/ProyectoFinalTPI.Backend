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
        private readonly IFotoPerfilAlmacenamiento _fotoPerfilAlmacenamiento;

        public UsuarioServicio(
            IUsuarioRepositorio repositorio,
            ILogroServicio logroServicio,
            ISeguimientoServicio? seguimientoServicio = null, TimeProvider? timeProvider = null,
            IFotoPerfilAlmacenamiento? fotoPerfilAlmacenamiento = null)
        {
            _repositorio = repositorio;
            _seguimientoServicio = seguimientoServicio;
            _logroServicio = logroServicio;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _fotoPerfilAlmacenamiento = fotoPerfilAlmacenamiento ?? new FotoPerfilNoConfigurada();
        }

        public async Task<UsuarioPerfilDto> CambiarUsernamePropioAsync(
            int usuarioId, string username, CancellationToken cancellationToken = default)
        {
            var nuevoUsername = username?.Trim() ?? string.Empty;
            if (nuevoUsername.Length is < 3 or > 50)
                throw new ArgumentException("El username debe tener entre 3 y 50 caracteres.", nameof(username));

            var usuario = await _repositorio.ObtenerPorIdAsync(usuarioId)
                ?? throw new KeyNotFoundException("El usuario no existe.");

            if (!string.Equals(usuario.Username, nuevoUsername, StringComparison.Ordinal))
            {
                var existente = await _repositorio.ObtenerPorUsernameAsync(nuevoUsername, cancellationToken);
                if (existente is not null && existente.Id != usuarioId)
                    throw new NombreUsuarioEnUsoException();

                usuario.Username = nuevoUsername;
                await _repositorio.ActualizarAsync(usuario, cancellationToken);
            }

            return await ConstruirPerfilDto(usuario, usuarioId, cancellationToken);
        }

        public async Task<UsuarioPerfilDto> CambiarFotoPerfilPropiaAsync(
            int usuarioId, Stream archivo, string contentType, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(archivo);
            var usuario = await _repositorio.ObtenerPorIdAsync(usuarioId)
                ?? throw new KeyNotFoundException("El usuario no existe.");

            const int maxBytes = 2 * 1024 * 1024;
            using var memoria = new MemoryStream();
            var buffer = new byte[81920];
            int leidos;
            while ((leidos = await archivo.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (memoria.Length + leidos > maxBytes)
                    throw new ArgumentException("La foto no puede superar 2 MiB.", nameof(archivo));
                await memoria.WriteAsync(buffer.AsMemory(0, leidos), cancellationToken);
            }

            var bytes = memoria.ToArray();
            var extension = DetectarExtension(bytes);
            if (extension is null)
                throw new ArgumentException("La foto debe ser JPEG, PNG o WebP válido.", nameof(archivo));

            var tipoEsperado = extension switch
            {
                ".jpg" => "image/jpeg",
                ".png" => "image/png",
                _ => "image/webp"
            };
            if (!string.Equals(contentType, tipoEsperado, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("El tipo de contenido no coincide con la foto.", nameof(contentType));

            using var contenido = new MemoryStream(bytes);
            var url = await _fotoPerfilAlmacenamiento.SubirAsync(
                contenido, $"usuario-{usuarioId}-{Guid.NewGuid():N}{extension}", cancellationToken);
            usuario.FotoPerfilUrl = url;
            await _repositorio.ActualizarAsync(usuario, cancellationToken);
            return await ConstruirPerfilDto(usuario, usuarioId, cancellationToken);
        }

        private static string? DetectarExtension(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return ".jpg";
            if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                return ".png";
            if (bytes.Length >= 12
                && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
                return ".webp";
            return null;
        }

        private sealed class FotoPerfilNoConfigurada : IFotoPerfilAlmacenamiento
        {
            public Task<string> SubirAsync(Stream archivo, string nombreArchivo, CancellationToken cancellationToken)
                => throw new InvalidOperationException("No se configuró el almacenamiento de fotos de perfil.");
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

        public async Task<IReadOnlyList<AutorResumenDto>> BuscarAsync(
            string texto, int limite = 10, CancellationToken cancellationToken = default)
        {
            var busqueda = texto?.Trim() ?? string.Empty;
            if (busqueda.Length < 2 || busqueda.Length > 50)
                throw new ArgumentException("El texto de búsqueda debe tener entre 2 y 50 caracteres.", nameof(texto));
            if (limite < 1 || limite > 20)
                throw new ArgumentOutOfRangeException(nameof(limite), "El límite debe estar entre 1 y 20.");

            return await _repositorio.BuscarPorUsernameAsync(busqueda, limite, cancellationToken);
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
                FotoPerfilUrl = usuario.FotoPerfilUrl,
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
                var reputacion = personal.Reputacion;
                if (reputacion == ReputacionEnum.Errante && cantidadPublicaciones > 0)
                {
                    reputacion = ReputacionEnum.Nostalgico;
                }
                dto.Reputacion = FormatearReputacion(reputacion);
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
