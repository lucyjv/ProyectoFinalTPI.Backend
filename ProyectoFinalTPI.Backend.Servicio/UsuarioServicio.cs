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
        private readonly IPerfilPublicoConsulta? _perfilPublico;
        private readonly ISeguimientoServicio? _seguimientoServicio;
        private readonly ILogroServicio _logroServicio;
        private readonly TimeProvider _timeProvider;
        private readonly IFotoPerfilAlmacenamiento _fotoPerfilAlmacenamiento;

        public UsuarioServicio(
            IUsuarioRepositorio repositorio,
            ILogroServicio logroServicio,
            ISeguimientoServicio? seguimientoServicio = null, TimeProvider? timeProvider = null,
            IFotoPerfilAlmacenamiento? fotoPerfilAlmacenamiento = null, IPerfilPublicoConsulta? perfilPublico = null)
        {
            _repositorio = repositorio;
            _perfilPublico = perfilPublico;
            _seguimientoServicio = seguimientoServicio;
            _logroServicio = logroServicio;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _fotoPerfilAlmacenamiento = fotoPerfilAlmacenamiento ?? new FotoPerfilNoConfigurada();
        }

        #region Operaciones públicas

        /// <inheritdoc/>
        public async Task<UsuarioResumenDto> ObtenerPorUsernameAsync(
            string username, CancellationToken cancellationToken = default)
        {
            var usuario = await ObtenerUsuarioPorUsername(username, cancellationToken);

            return new UsuarioResumenDto
            {
                UsuarioId = usuario.Id,
                Username = usuario.Username
            };
        }
        public async Task<IReadOnlyList<AutorResumenDto>> BuscarAsync(
            string texto, int limite = 10, CancellationToken cancellationToken = default)
        {
            string busqueda = ValidarBusqueda(texto, limite);

            return await _repositorio.BuscarPorUsernameAsync(busqueda, limite, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<UsuarioPerfilDto> ObtenerPerfilPorUsernameAsync(
            string username, int? usuarioAutenticadoId, CancellationToken cancellationToken = default)
        {
            var usuario = await ObtenerUsuarioPorUsername(username, cancellationToken);
            if (_perfilPublico is not null) usuario = await _perfilPublico.Obtener(usuario.Id, cancellationToken) ?? usuario;
            return await ConstruirPerfilDto(usuario, usuarioAutenticadoId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<UsuarioPerfilDto> ObtenerPerfilPorIdAsync(
            int usuarioId, int? usuarioAutenticadoId, CancellationToken cancellationToken = default)
        {
            var usuario = _perfilPublico is null ? await _repositorio.ObtenerPorIdAsync(usuarioId) : await _perfilPublico.Obtener(usuarioId, cancellationToken);

            if (usuario is null)
                throw new KeyNotFoundException($"No existe ningún usuario con el identificador '{usuarioId}'.");

            return await ConstruirPerfilDto(usuario, usuarioAutenticadoId, cancellationToken);
        }

        public async Task<List<LogroEstadoDto>> RegistrarIngresoDiarioAsync(int usuarioId, CancellationToken cancellationToken = default)
        {
            var usuario = await _repositorio.ObtenerYUsuarioInteractivoPorIdAsync(usuarioId);
            if (usuario == null) return new();

            var ahora = _timeProvider.GetUtcNow().UtcDateTime;
            if (usuario.UltimoIngresoUtc?.Date != ahora.Date)
            {
                ActualizarRacha(usuario, ahora);
                await _repositorio.ActualizarAsync(usuario, cancellationToken);
            }

            return await ValidarLogrosDeRacha(usuarioId, usuario);
        }

        public async Task<UsuarioPerfilDto> CambiarUsernamePropioAsync(
            int usuarioId, string username, CancellationToken cancellationToken = default)
        {
            var nuevoUsername = ValidarNuevoUsername(username);
            var usuario = await ObtenerUsuarioPropio(usuarioId);
            await ActualizarUsernameSiCambio(usuario, usuarioId, nuevoUsername, cancellationToken);
            return await ConstruirPerfilDto(usuario, usuarioId, cancellationToken);
        }

        public async Task<UsuarioPerfilDto> CambiarFotoPerfilPropiaAsync(
            int usuarioId, Stream archivo, string contentType, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(archivo);
            var usuario = await ObtenerUsuarioPropio(usuarioId);
            var bytes = await LeerFotoConLimite(archivo, cancellationToken);
            var extension = ValidarFormatoFoto(bytes, contentType);
            await GuardarFotoPerfil(usuario, usuarioId, bytes, extension, cancellationToken);
            return await ConstruirPerfilDto(usuario, usuarioId, cancellationToken);
        }

        #endregion

        #region Métodos privados y validaciones

        private async Task<List<LogroEstadoDto>> ValidarLogrosDeRacha(int usuarioId, Usuario_Interactivo usuario)
        {
            var nuevos = new List<int>();
            if (usuario.RachaDiasActual >= 3 && await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.RachaDeHierro))
                nuevos.Add((int)LogroEnum.RachaDeHierro);

            if (usuario.RachaDiasActual >= 7 && await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.RachaDeOro))
                nuevos.Add((int)LogroEnum.RachaDeOro);
            return nuevos.Count == 0 ? new() : (await _logroServicio.ObtenerCatalogoParaUsuario(usuarioId))
                .Where(logro => nuevos.Contains(logro.Id)).ToList();
        }
        private static void ActualizarRacha(Usuario_Interactivo usuario, DateTime ahora)
        {
            var ultimoIngreso = usuario.UltimoIngresoUtc?.Date;
            usuario.RachaDiasActual = ultimoIngreso.HasValue && ultimoIngreso.Value == ahora.Date.AddDays(-1)
                ? usuario.RachaDiasActual + 1
                : 1;
            usuario.UltimoIngresoUtc = ahora;
        }
  
        private async Task<Usuario> ObtenerUsuarioPorUsername(string username, CancellationToken cancellationToken)
        {
            return await _repositorio.ObtenerPorUsernameAsync(username, cancellationToken)
                ?? throw new KeyNotFoundException($"No existe ningún usuario con el username '{username}'.");
        }
        private static string ValidarBusqueda(string texto, int limite)
        {
            var busqueda = texto?.Trim() ?? string.Empty;
            if (busqueda.Length < 2 || busqueda.Length > 50)
                throw new ArgumentException("El texto de búsqueda debe tener entre 2 y 50 caracteres.", nameof(texto));
            if (limite < 1 || limite > 20)
                throw new ArgumentOutOfRangeException(nameof(limite), "El límite debe estar entre 1 y 20.");
            return busqueda;
        }

        private static string FormatearReputacion(ReputacionEnum reputacion) => reputacion switch
        {
            ReputacionEnum.Errante => "Errante",
            ReputacionEnum.Nostalgico => "Nostálgico",
            ReputacionEnum.Aedo => "Aedo",
            ReputacionEnum.Mnnemónide => "Mnemosine",
            _ => "Errante"
        };

        private async Task<UsuarioPerfilDto> ConstruirPerfilDto(Usuario usuario, int? usuarioAutenticadoId, CancellationToken cancellationToken)
        {
            var cantidad = await _repositorio.ContarPublicacionesVisiblesAsync(usuario.Id, cancellationToken);
            var seguimiento = await ObtenerResumenSeguimiento(usuario.Id, usuarioAutenticadoId, cancellationToken);
            var logros = await _logroServicio.ObtenerCatalogoParaUsuario(usuario.Id);
            var dto = CrearPerfilBase(usuario, cantidad, seguimiento, logros);
            CompletarDatosPorTipo(dto, usuario, cantidad);
            return dto;
        }
        private async Task<Usuario> ObtenerUsuarioPropio(int usuarioId) => await _repositorio.ObtenerPorIdAsync(usuarioId)
        ?? throw new KeyNotFoundException("El usuario no existe.");

        private static string ValidarNuevoUsername(string username)
        {
            var nuevo = username?.Trim() ?? string.Empty;
            if (nuevo.Length is < 3 or > 50)
                throw new ArgumentException("El username debe tener entre 3 y 50 caracteres.", nameof(username));
            return nuevo;
        }

        private async Task ActualizarUsernameSiCambio(
            Usuario usuario, int usuarioId, string nuevo, CancellationToken cancellationToken)
        {
            if (string.Equals(usuario.Username, nuevo, StringComparison.Ordinal)) return;
            var existente = await _repositorio.ObtenerPorUsernameAsync(nuevo, cancellationToken);
            if (existente is not null && existente.Id != usuarioId) throw new NombreUsuarioEnUsoException();
            usuario.Username = nuevo;
            await _repositorio.ActualizarAsync(usuario, cancellationToken);
        }

        private static async Task<byte[]> LeerFotoConLimite(Stream archivo, CancellationToken cancellationToken)
        {
            using var memoria = new MemoryStream();
            var buffer = new byte[81920];
            int leidos;
            while ((leidos = await archivo.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (memoria.Length + leidos > 2 * 1024 * 1024)
                    throw new ArgumentException("La foto no puede superar 2 MiB.", nameof(archivo));
                await memoria.WriteAsync(buffer.AsMemory(0, leidos), cancellationToken);
            }
            return memoria.ToArray();
        }

        private static string ValidarFormatoFoto(byte[] bytes, string contentType)
        {
            var extension = DetectarExtension(bytes)
                ?? throw new ArgumentException("La foto debe ser JPEG, PNG o WebP válido.", "archivo");
            var tipo = extension switch { ".jpg" => "image/jpeg", ".png" => "image/png", _ => "image/webp" };
            if (!string.Equals(contentType, tipo, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("El tipo de contenido no coincide con la foto.", nameof(contentType));
            return extension;
        }

        private async Task GuardarFotoPerfil(
            Usuario usuario, int usuarioId, byte[] bytes, string extension, CancellationToken cancellationToken)
        {
            using var contenido = new MemoryStream(bytes);
            usuario.FotoPerfilUrl = await _fotoPerfilAlmacenamiento.SubirAsync(
                contenido, $"usuario-{usuarioId}-{Guid.NewGuid():N}{extension}", cancellationToken);
            await _repositorio.ActualizarAsync(usuario, cancellationToken);
        }

        private async Task<(int Seguidores, int Seguidos, bool LoSigo)> ObtenerResumenSeguimiento(
            int usuarioId, int? usuarioAutenticadoId, CancellationToken cancellationToken)
        {
            if (_seguimientoServicio is null) return (0, 0, false);
            var seguidores = await _seguimientoServicio.ListarSeguidores(usuarioId, cancellationToken);
            var seguidos = await _seguimientoServicio.ListarSeguidos(usuarioId, cancellationToken);
            var loSigo = await ConsultarSiLoSigo(usuarioId, usuarioAutenticadoId, cancellationToken);
            return (seguidores?.Count ?? 0, seguidos?.Count ?? 0, loSigo);
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
        private async Task<bool> ConsultarSiLoSigo(
    int usuarioId, int? usuarioAutenticadoId, CancellationToken cancellationToken)
        {
            if (!usuarioAutenticadoId.HasValue || usuarioAutenticadoId.Value <= 0
                || usuarioAutenticadoId.Value == usuarioId) return false;
            var seguidos = await _seguimientoServicio!.ObtenerSeguidosIdsAsync(
                usuarioAutenticadoId.Value, cancellationToken);
            return seguidos.Contains(usuarioId);
        }

        private static UsuarioPerfilDto CrearPerfilBase(
            Usuario usuario, int cantidad, (int Seguidores, int Seguidos, bool LoSigo) seguimiento,
            List<LogroEstadoDto> logros) => new()
            {
                Id = usuario.Id,
                Username = usuario.Username,
                FotoPerfilUrl = usuario.FotoPerfilUrl,
                CantidadPublicaciones = cantidad,
                CantidadSeguidores = seguimiento.Seguidores,
                CantidadSeguidos = seguimiento.Seguidos,
                LoSigo = seguimiento.LoSigo,
                Logros = logros
            };

        private static void CompletarDatosPorTipo(UsuarioPerfilDto dto, Usuario usuario, int cantidad)
        {
            switch (usuario)
            {
                case Usuario_Personal personal:
                    CompletarPerfilPersonal(dto, personal, cantidad);
                    break;
                case Usuario_Marca marca:
                    dto.TipoUsuario = "Usuario_Marca";
                    dto.Nombre = marca.NombreEmpresa;
                    dto.Reputacion = "Marca";
                    dto.PuntosNostalgia = marca.PuntosNostalgia;
                    break;
                default:
                    dto.TipoUsuario = "Usuario";
                    dto.Reputacion = "Errante";
                    break;
            }
        }

        private static void CompletarPerfilPersonal(UsuarioPerfilDto dto, Usuario_Personal usuario, int cantidad)
        {
            dto.TipoUsuario = "Usuario_Personal";
            dto.Nombre = usuario.Nombre;
            dto.Apellido = usuario.Apellido;
            var reputacion = usuario.Reputacion == ReputacionEnum.Errante && cantidad > 0
                ? ReputacionEnum.Nostalgico : usuario.Reputacion;
            dto.Reputacion = FormatearReputacion(reputacion);
            dto.PuntosNostalgia = usuario.PuntosNostalgia;
        }

        #endregion
    }
}
