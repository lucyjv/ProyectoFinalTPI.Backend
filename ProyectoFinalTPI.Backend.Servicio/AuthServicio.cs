using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BC = BCrypt.Net.BCrypt;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class AuthServicio : IAuthServicio
    {
        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IConfiguration _configuration;

        public AuthServicio(IUsuarioRepositorio usuarioRepositorio, IConfiguration configuration)
        {
            _usuarioRepositorio = usuarioRepositorio;
            _configuration = configuration;
        }

        public async Task<AuthRespuestaDto> RegistrarAsync(RegistroDto dto)
        {
            var emailEnUso = await _usuarioRepositorio.ExistePorEmailAsync(dto.Email);

            if (emailEnUso)
                throw new InvalidOperationException("Ya existe una cuenta registrada con ese email.");

            var usuario = new Usuario_Personal
            {
                Username = dto.Username,
                Email = dto.Email,
                Contraseña = BC.HashPassword(dto.Contraseña),
                EsAdmin = false,
                FechaCreacion = DateTime.UtcNow
            };

            await _usuarioRepositorio.AgregarAsync(usuario);

            return GenerarRespuesta(usuario);
        }

        public async Task<AuthRespuestaDto> LoginAsync(LoginDto dto)
        {
            var usuario = await _usuarioRepositorio.ObtenerPorEmailAsync(dto.Email);

            if (usuario is null)
                throw new UnauthorizedAccessException("Email o contraseña incorrectos.");

            bool passwordValida = false;
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
                    await _usuarioRepositorio.ActualizarAsync(usuario);
                }
            }

            if (!passwordValida)
                throw new UnauthorizedAccessException("Email o contraseña incorrectos.");

            return GenerarRespuesta(usuario);
        }

        private AuthRespuestaDto GenerarRespuesta(Usuario usuario)
        {
            return new AuthRespuestaDto
            {
                Token = GenerarJwt(usuario),
                UsuarioId = usuario.Id,
                Username = usuario.Username,
                Email = usuario.Email,
                EsAdmin = usuario.EsAdmin
            };
        }

        private string GenerarJwt(Usuario usuario)
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
                new Claim("esAdmin", usuario.EsAdmin.ToString().ToLower()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Emisor"] ?? "nostalgia-backend",
                audience: _configuration["Jwt:Audiencia"] ?? "nostalgia-frontend",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credenciales
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
