using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ProyectoFinalTPI.Backend.Dtos.Auth;

namespace ProyectoFinalTPI.Backend;

public sealed class GoogleNoDisponibleException() : Exception("El acceso con Google no está disponible en este momento. Intentá nuevamente más tarde.");

public interface IGoogleIdentityVerifier
{
    Task<IdentidadGoogle> VerificarAsync(string credential, string nonce, CancellationToken cancellationToken);
}

/// <summary>Validate Google's OIDC signature and claims locally using its rotating public keys.</summary>
public sealed class GoogleIdentityVerifier(IConfiguration configuration, IConfigurationManager<OpenIdConnectConfiguration>? configurationManager = null) : IGoogleIdentityVerifier
{
    private static readonly ConfigurationManager<OpenIdConnectConfiguration> Metadata = new(
        "https://accounts.google.com/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever());

    public async Task<IdentidadGoogle> VerificarAsync(string credential, string nonce, CancellationToken cancellationToken)
    {
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId)) throw new GoogleNoDisponibleException();
        var manager = configurationManager ?? Metadata;
        OpenIdConnectConfiguration metadata;
        try { metadata = await manager.GetConfigurationAsync(cancellationToken); }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        { throw new GoogleNoDisponibleException(); }
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            handler.ValidateToken(credential, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, IssuerSigningKeys = metadata.SigningKeys,
                ValidateIssuer = true, ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                ValidateAudience = true, ValidAudience = clientId,
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromSeconds(30)
            }, out var validated);
            var jwt = (JwtSecurityToken)validated;
            if (jwt.Audiences.Count() != 1) throw new SecurityTokenValidationException("Invalid audience count.");
            if (jwt.Payload.TryGetValue("azp", out var azp) && azp?.ToString() != clientId)
                throw new SecurityTokenValidationException("Invalid authorized party.");
            var subject = jwt.Subject;
            var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var verified = jwt.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value;
            var tokenNonce = jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
            if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255 || string.IsNullOrWhiteSpace(email)
                || email.Length > 200 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)
                || !string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase) || tokenNonce != nonce)
                throw new SecurityTokenValidationException("Invalid Google identity.");
            var domain = email[(email.LastIndexOf('@') + 1)..];
            var hostedDomain = jwt.Claims.FirstOrDefault(c => c.Type == "hd")?.Value;
            var authoritative = domain.Equals("gmail.com", StringComparison.OrdinalIgnoreCase)
                || domain.Equals("googlemail.com", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(hostedDomain) && domain.Equals(hostedDomain, StringComparison.OrdinalIgnoreCase));
            return new IdentidadGoogle(subject, email, authoritative);
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            manager.RequestRefresh();
            throw new UnauthorizedAccessException("No pudimos validar Google. Volvé a intentar.");
        }
        catch (SecurityTokenException)
        {
            throw new UnauthorizedAccessException("La respuesta de Google no es válida o expiró. Volvé a intentar.");
        }
        catch (ArgumentException)
        {
            throw new UnauthorizedAccessException("La respuesta de Google no es válida.");
        }
    }
}
