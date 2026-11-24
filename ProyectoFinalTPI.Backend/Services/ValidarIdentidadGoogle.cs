using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ProyectoFinalTPI.Backend.Dtos.Auth;

namespace ProyectoFinalTPI.Backend.Services;

public sealed class ValidarIdentidadGoogle(IConfiguration configuracion,
    IConfigurationManager<OpenIdConnectConfiguration>? configuracionGoogle = null)
{
    private static readonly string[] Emisores = ["https://accounts.google.com", "accounts.google.com"];
    private readonly string? _clientId = configuracion["GoogleAuth:ClientId"]?.Trim();
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configuracionGoogle =
        configuracionGoogle ?? new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://accounts.google.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());

    public bool Habilitado => !string.IsNullOrEmpty(_clientId);

    public async Task<IdentidadGoogleDto> ValidarAsync(string credential, string nonce, CancellationToken ct)
    {
        var metadata = await _configuracionGoogle.GetConfigurationAsync(ct);
        var parametros = new TokenValidationParameters
        {
            ValidIssuers = Emisores,
            ValidAudience = _clientId,
            IssuerSigningKeys = metadata.SigningKeys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            IgnoreTrailingSlashWhenValidatingAudience = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        try
        {
            var claims = new JwtSecurityTokenHandler { MapInboundClaims = false }
                .ValidateToken(credential, parametros, out _);
            var subject = claims.FindFirst("sub")?.Value;
            var email = claims.FindFirst("email")?.Value;
            if (claims.FindFirst("nonce")?.Value != nonce ||
                !bool.TryParse(claims.FindFirst("email_verified")?.Value, out var verificado) || !verificado ||
                string.IsNullOrWhiteSpace(subject) || subject.Length > 255 ||
                string.IsNullOrWhiteSpace(email) || email.Length > 200 || !new EmailAddressAttribute().IsValid(email))
                throw new SecurityTokenValidationException("Identidad de Google inválida.");

            return new IdentidadGoogleDto(subject, email);
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            _configuracionGoogle.RequestRefresh();
            throw;
        }
    }
}
