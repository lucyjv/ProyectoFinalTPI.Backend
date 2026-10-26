using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BC = BCrypt.Net.BCrypt;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AuthServicio : IAuthServicio
{
    private readonly IUsuarioRepositorio _usuarioRepositorio;
    private readonly ISesionUsuarioRepositorio _sesionRepositorio;
    private readonly IConfiguration _configuration;
    private readonly IUsuarioServicio _usuarioServicio;

    public AuthServicio(
        IUsuarioRepositorio usuarioRepositorio,
        ISesionUsuarioRepositorio sesionRepositorio,
        IConfiguration configuration, IUsuarioServicio usuarioServicio)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _sesionRepositorio = sesionRepositorio;
        _configuration = configuration;
        _usuarioServicio = usuarioServicio;
    }

    public async Task<ResultadoAutenticacionDto> RegistrarAsync(
        RegistroDto dto,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
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

        await _usuarioRepositorio.AgregarAsync(usuario, cancellationToken);
        return await CrearSesionAsync(usuario, userAgent, cancellationToken);
    }

    public async Task<ResultadoAutenticacionDto> LoginAsync(
        LoginDto dto,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepositorio.ObtenerPorEmailAsync(NormalizarEmail(dto.Email), cancellationToken);
        if (usuario is null)
            throw new UnauthorizedAccessException("Email o contraseña incorrectos.");

        var passwordValida = false;
        try
        {
            passwordValida = BC.Verify(dto.Contraseña, usuario.Contraseña);
        }
        catch
        {
            if (dto.Contraseña == usuario.Contraseña)
            {
                passwordValida = true;
                usuario.Contraseña = BC.HashPassword(dto.Contraseña);
                await _usuarioRepositorio.ActualizarAsync(usuario, cancellationToken);
            }
        }

        if (!passwordValida)
            throw new UnauthorizedAccessException("Email o contraseña incorrectos.");

        return await CrearSesionAsync(usuario, userAgent, cancellationToken);
    }

    public async Task<ResultadoAutenticacionDto> RenovarAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (!TryObtenerSesionId(refreshToken, out var sesionId))
            throw new UnauthorizedAccessException("La sesión no es válida.");

        var sesion = await _sesionRepositorio.ObtenerPorIdAsync(sesionId, cancellationToken);
        if (sesion is null || !sesion.EstaActiva || !HashCoincide(refreshToken, sesion.RefreshTokenHash))
            throw new UnauthorizedAccessException("La sesión no es válida o expiró.");

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

        var nuevos = await _usuarioServicio.RegistrarIngresoDiarioAsync(sesion.UsuarioId, cancellationToken);
        var resultado = CrearResultado(sesion.Usuario, sesion.Id, nuevoRefreshToken, nuevaExpiracion);
        resultado.Respuesta.LogrosNuevos = nuevos ?? new();
        return resultado;
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

    private static bool TryObtenerSesionId(string token, out Guid sesionId)
    {
        sesionId = Guid.Empty;
        var separador = token.IndexOf('.');
        return separador == 32 && Guid.TryParseExact(token[..separador], "N", out sesionId);
    }
}
