using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProyectoFinalTPI.Backend.Tests;

// Solo los tests anteriores de formularios y reposts registran este esquema.
// Los tests de categorías siguen usando el registro, JWT y sesiones reales.
internal static class AutenticacionPruebas
{
    public static void Registrar(IServiceCollection servicios) => servicios
        .AddAuthentication(opciones => { opciones.DefaultAuthenticateScheme = "pruebas"; opciones.DefaultChallengeScheme = "pruebas"; })
        .AddScheme<AuthenticationSchemeOptions, ManejadorPruebas>("pruebas", _ => { });

    public static HttpClient CrearCliente(WebApplicationFactory<Program> factory, int usuarioId)
    {
        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Usuario-Prueba", usuarioId.ToString());
        return cliente;
    }
}

internal sealed class ManejadorPruebas(IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!int.TryParse(Request.Headers["X-Usuario-Prueba"], out var id))
            return Task.FromResult(AuthenticateResult.NoResult());
        var identidad = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Scheme.Name)));
    }
}
