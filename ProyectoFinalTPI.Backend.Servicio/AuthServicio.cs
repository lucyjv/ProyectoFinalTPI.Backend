using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using BC = BCrypt.Net.BCrypt;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AuthServicio : IAuthServicio, IProcesadorRecuperacionContrasena
{
    private readonly IUsuarioRepositorio _usuarioRepositorio;
    private readonly ISesionUsuarioRepositorio _sesionRepositorio;
    private readonly IConfiguration _configuration;
    private readonly IUsuarioServicio _usuarioServicio;
    private readonly IRecuperacionContrasenaRepositorio _recuperaciones;
    private readonly IColaRecuperacionContrasena _correosPendientes;
    private readonly RecuperacionContrasenaOptions _recuperacionOptions;
    private readonly TimeProvider _reloj;

    public AuthServicio(
        IUsuarioRepositorio usuarioRepositorio,
        ISesionUsuarioRepositorio sesionRepositorio,
        IConfiguration configuration, IUsuarioServicio usuarioServicio,
        IRecuperacionContrasenaRepositorio recuperaciones, IColaRecuperacionContrasena correosPendientes,
        IOptions<RecuperacionContrasenaOptions> recuperacionOptions, TimeProvider reloj)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _sesionRepositorio = sesionRepositorio;
        _configuration = configuration;
        _usuarioServicio = usuarioServicio;
        _recuperaciones = recuperaciones;
        _correosPendientes = correosPendientes;
        _recuperacionOptions = recuperacionOptions.Value;
        _reloj = reloj;
    }

    #region Operaciones públicas

    public async Task<ResultadoAutenticacionDto> RegistrarAsync(
        RegistroDto dto,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {     
        var usuario = await PrepararUsuarioParaRegistro(dto, cancellationToken);
        await _usuarioRepositorio.AgregarAsync(usuario, cancellationToken);
        return await CrearSesionAsync(usuario, userAgent, cancellationToken);
    }

    public async Task<ResultadoAutenticacionDto> LoginAsync(
        LoginDto dto,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ObtenerUsuarioParaLogin(dto.Email, cancellationToken);
        await ValidarCredenciales(usuario, dto.Contraseña, cancellationToken);

        return await CrearSesionAsync(usuario, userAgent, cancellationToken);
    }
    public async Task<ResultadoAutenticacionDto> RenovarAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var sesion = await ObtenerSesionParaRenovar(refreshToken, cancellationToken);
        var (nuevoRefreshToken, nuevaExpiracion) = await RotarSesion(sesion, cancellationToken);
        var nuevos = await _usuarioServicio.RegistrarIngresoDiarioAsync(sesion.UsuarioId, cancellationToken);
        return CrearResultado(sesion.Usuario, sesion.Id, nuevoRefreshToken, nuevaExpiracion, nuevos);
    }

    public async Task CerrarSesionAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || !TryObtenerSesionId(refreshToken, out var sesionId))
            return;

        var sesion = await _sesionRepositorio.ObtenerPorIdAsync(sesionId, cancellationToken);
        if (sesion is null || !HashCoincide(refreshToken, sesion.RefreshTokenHash))
            return;

        await _sesionRepositorio.RevocarAsync(sesion, DateTime.UtcNow, cancellationToken);
    }

    public Task CerrarTodasLasSesionesAsync(int usuarioId, CancellationToken cancellationToken = default) =>
        _sesionRepositorio.RevocarTodasAsync(usuarioId, DateTime.UtcNow, cancellationToken);

    public Task SolicitarRecuperacionAsync(RecuperarContrasenaDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        VerificarRecuperacionHabilitada();
        Validator.ValidateObject(dto, new ValidationContext(dto), validateAllProperties: true);

        // dejamos en cola el envio del correo de recuperación, que se procesa ean segundo plano
        if (!_correosPendientes.EncolarSolicitud(NormalizarEmail(dto.Email)))
            throw new RecuperacionContrasenaNoDisponibleException();
        return Task.CompletedTask;
    }

    public async Task<CorreoElectronicoDto?> PrepararRecuperacionAsync(string email, CancellationToken cancellationToken = default)
    {
        if (!_recuperacionOptions.Habilitado) return null;
        var usuario = await _usuarioRepositorio.ObtenerPorEmailAsync(NormalizarEmail(email), cancellationToken);
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        if (!CuentaHabilitada(usuario, ahora)) return null;

        var recuperacion = new RecuperacionContrasena
        {
            UsuarioId = usuario!.Id, EmailDestino = NormalizarEmail(usuario.Email),
            FechaCreacion = ahora, FechaExpiracion = ahora.AddMinutes(_recuperacionOptions.ValidezMinutos)
        };
        var token = $"{recuperacion.Id:N}.{Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32))}";
        recuperacion.TokenHash = HashearToken(token);

        var creada = await _recuperaciones.CrearSiCorrespondeAsync(recuperacion,
            TimeSpan.FromSeconds(_recuperacionOptions.IntervaloSolicitudesSegundos), cancellationToken);
        if (!creada) return null;

        var enlace = $"{_recuperacionOptions.UrlRestablecimiento}#token={Uri.EscapeDataString(token)}";
        return CorreosRecuperacion.CrearEnlace(recuperacion.EmailDestino, enlace,
            _recuperacionOptions.ValidezMinutos, recuperacion.Id);
    }

    public async Task RestablecerContrasenaAsync(RestablecerContrasenaDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        VerificarRecuperacionHabilitada();
        Validator.ValidateObject(dto, new ValidationContext(dto), validateAllProperties: true);

        if (!TryLeerTokenRecuperacion(dto.Token, out var id)) throw EnlaceNoValido();
        var recuperacion = await _recuperaciones.ObtenerPorIdAsync(id, cancellationToken);
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        if (recuperacion is null || recuperacion.FechaUso is not null || recuperacion.FechaRevocacion is not null
            || recuperacion.FechaExpiracion <= ahora || !CuentaHabilitada(recuperacion.Usuario, ahora)
            || !HashCoincide(dto.Token, recuperacion.TokenHash))
            throw EnlaceNoValido();

        var actualizada = await _recuperaciones.RestablecerAsync(id,
                                                HashearToken(dto.Token),
                                                BC.HashPassword(dto.NuevaContrasena),
                                                cancellationToken);

        if (!actualizada) throw EnlaceNoValido();

        _correosPendientes.EncolarAviso(recuperacion.EmailDestino, id);
    }

    private void VerificarRecuperacionHabilitada()
    {
        if (!_recuperacionOptions.Habilitado) throw new RecuperacionContrasenaNoDisponibleException();
    }

    private static ArgumentException EnlaceNoValido() => new("El enlace no es válido, venció o ya fue utilizado.");

    private static bool CuentaHabilitada(Usuario? usuario, DateTime ahora) => usuario is not null
        && !usuario.EstaEliminado
        && !(usuario is Usuario_Interactivo { SuspendidoHasta: { } hasta } && hasta > ahora);

    private static bool TryLeerTokenRecuperacion(string token, out Guid id)
    {
        id = Guid.Empty;
        return token.Length == 76 && token[32] == '.' && Guid.TryParseExact(token[..32], "N", out id)
            && token.Skip(33).All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    }
    
    #endregion

    #region Métodos privados y validaciones
    private static bool TryObtenerSesionId(string token, out Guid sesionId)
    {
        sesionId = Guid.Empty;
        var separador = token.IndexOf('.');
        return separador == 32 && Guid.TryParseExact(token[..separador], "N", out sesionId);
    }
    private async Task<ResultadoAutenticacionDto> CrearSesionAsync(
        Usuario usuario,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var nuevos = await _usuarioServicio.RegistrarIngresoDiarioAsync(usuario.Id, cancellationToken);
        var expiracion = DateTime.UtcNow.AddDays(ObtenerRefreshTokenDias());
        var sesion = new SesionUsuario
        {
            UsuarioId = usuario.Id,
            Usuario = usuario,
            FechaExpiracion = expiracion,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, 512)]
        };
        var refreshToken = GenerarRefreshToken(sesion.Id);
        sesion.RefreshTokenHash = HashearToken(refreshToken);
        await _sesionRepositorio.AgregarAsync(sesion, cancellationToken);
        var resultado = CrearResultado(usuario, sesion.Id, refreshToken, expiracion);
        resultado.Respuesta.LogrosNuevos = nuevos ?? new();
        return resultado;
    }
    private ResultadoAutenticacionDto CrearResultado(Usuario usuario, Guid sesionId, string refreshToken, DateTime expiracion) =>
        new()
        {
            Respuesta = new AuthRespuestaDto
            {
                Token = GenerarJwt(usuario, sesionId),
                UsuarioId = usuario.Id,
                Username = usuario.Username,
                FotoPerfilUrl = usuario.FotoPerfilUrl,
                Email = usuario.Email,
                EsAdmin = usuario.EsAdmin
            },
            RefreshToken = refreshToken,
            RefreshTokenExpira = expiracion
        };
    private string GenerarJwt(Usuario usuario, Guid sesionId)
    {
        var claveSecreta = _configuration["Jwt:Clave"]
            ?? "clave-de-testing-no-usar-en-produccion-32ch";
        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claveSecreta));
        var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim("username", usuario.Username),
            new Claim("esAdmin", usuario.EsAdmin.ToString().ToLowerInvariant()),
            new Claim("sid", sesionId.ToString("N")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Emisor"] ?? "nostalgia-backend",
            audience: _configuration["Jwt:Audiencia"] ?? "nostalgia-frontend",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(ObtenerAccessTokenMinutos()),
            signingCredentials: credenciales);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    private int ObtenerAccessTokenMinutos() =>
        int.TryParse(_configuration["Jwt:AccessTokenMinutes"], out var minutos) && minutos is >= 5 and <= 60 ? minutos : 15;
    private int ObtenerRefreshTokenDias() =>
        int.TryParse(_configuration["Jwt:RefreshTokenDays"], out var dias) && dias is >= 1 and <= 90 ? dias : 30;
    private static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();
    private static string GenerarRefreshToken(Guid sesionId) =>
        $"{sesionId:N}.{Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64))}";
    private static string HashearToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static bool HashCoincide(string token, string hashEsperado)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(HashearToken(token)),
                Convert.FromHexString(hashEsperado));
        }
        catch (FormatException)
        {
            return false;
        }
    }
    private async Task<Usuario_Personal> PrepararUsuarioParaRegistro(RegistroDto dto, CancellationToken cancellationToken)
    {
        var email = NormalizarEmail(dto.Email);
        if (await _usuarioRepositorio.ExistePorEmailAsync(email, cancellationToken))
            throw new InvalidOperationException("Ya existe una cuenta registrada con ese email.");

        var username = dto.Username.Trim();
        if (await _usuarioRepositorio.ExistePorUsernameAsync(username, cancellationToken))
            throw new InvalidOperationException("Ya existe una cuenta registrada con ese nombre de usuario.");

        var usuario = new Usuario_Personal
        {
            Username = username,
            Email = email,
            Contraseña = BC.HashPassword(dto.Contraseña),
            EsAdmin = false,
            FechaCreacion = DateTime.UtcNow
        };
        return usuario;
    }
    private async Task ValidarCredenciales(Usuario usuario, string contrasena, CancellationToken cancellationToken)
    {
        var contrasenaValida = false;
        try
        {
            contrasenaValida = BC.Verify(contrasena, usuario.Contraseña);
        }
        catch
        {
            if (contrasena == usuario.Contraseña)
            {
                contrasenaValida = true;
                usuario.Contraseña = BC.HashPassword(contrasena);
                await _usuarioRepositorio.ActualizarAsync(usuario, cancellationToken);
            }
        }

        if (!contrasenaValida)
            throw new UnauthorizedAccessException("Email o contraseña incorrectos.");
    }
    private async Task<Usuario> ObtenerUsuarioParaLogin(string email, CancellationToken cancellationToken)
    {
        var usuario = await _usuarioRepositorio.ObtenerPorEmailAsync(NormalizarEmail(email), cancellationToken);
        if (usuario is null)
            throw new UnauthorizedAccessException("Email o contraseña incorrectos.");
        if (usuario.EstaEliminado || usuario is Usuario_Interactivo { SuspendidoHasta: { } hasta } && hasta > DateTime.UtcNow)
            throw new UnauthorizedAccessException("La cuenta no está habilitada.");
        return usuario;
    }
    private async Task<SesionUsuario> ObtenerSesionParaRenovar(string refreshToken, CancellationToken cancellationToken)
    {
        if (!TryObtenerSesionId(refreshToken, out var sesionId))
            throw new UnauthorizedAccessException("La sesión no es válida.");

        var sesion = await _sesionRepositorio.ObtenerPorIdAsync(sesionId, cancellationToken);
        if (sesion is null || !sesion.EstaActiva || !HashCoincide(refreshToken, sesion.RefreshTokenHash))
            throw new UnauthorizedAccessException("La sesión no es válida o expiró.");

        return sesion;
    }
    private async Task<(string RefreshToken, DateTime Expiracion)> RotarSesion(
        SesionUsuario sesion, CancellationToken cancellationToken)
    {
        var ahora = DateTime.UtcNow;
        var nuevoRefreshToken = GenerarRefreshToken(sesion.Id);
        var nuevaExpiracion = ahora.AddDays(ObtenerRefreshTokenDias());
        var rotada = await _sesionRepositorio.RotarAsync(
            sesion,
            HashearToken(nuevoRefreshToken),
            nuevaExpiracion,
            ahora,
            cancellationToken);

        if (!rotada)
            throw new UnauthorizedAccessException("La sesión ya fue renovada por otra solicitud.");

        return (nuevoRefreshToken, nuevaExpiracion);
    }
    private ResultadoAutenticacionDto CrearResultado(
        Usuario usuario, Guid sesionId, string refreshToken, DateTime expiracion, List<LogroEstadoDto>? logrosNuevos) =>
        new()
        {
            Respuesta = new AuthRespuestaDto
            {
                Token = GenerarJwt(usuario, sesionId),
                UsuarioId = usuario.Id,
                Username = usuario.Username,
                Email = usuario.Email,
                EsAdmin = usuario.EsAdmin,
                LogrosNuevos = logrosNuevos ?? new()
            },
            RefreshToken = refreshToken,
            RefreshTokenExpira = expiracion
        };
    #endregion
}
