using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ProyectoFinalTPI.Backend.Tests;

internal static class AutenticacionPruebas
{
    public static HttpClient CrearCliente(WebApplicationFactory<Program> factory, int usuarioId)
    {
        var cliente = factory.CreateClient();
        var opciones = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
        var token = new JwtSecurityToken(opciones.ValidIssuer, opciones.ValidAudience,
            new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(opciones.IssuerSigningKey, SecurityAlgorithms.HmacSha256));
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return cliente;
    }
}