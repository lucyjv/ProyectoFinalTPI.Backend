using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Infraestructura;

public static class RegistroAnalisisRecuerdos
{
    public static IServiceCollection AddAnalisisRecuerdosIa(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddOptions<OpcionesAnalisisRecuerdos>()
            .Bind(configuracion.GetSection(OpcionesAnalisisRecuerdos.Seccion))
            .ValidateDataAnnotations()
            .Validate(o => string.Equals(o.Proveedor?.Trim(), "Gemini", StringComparison.OrdinalIgnoreCase),
                "AnalisisRecuerdos:Proveedor no es válido. El único proveedor implementado es Gemini.")
            .Validate(o => o.ReservaSegundos > o.TiempoMaximoSegundos + 5,
                "La reserva debe superar el tiempo máximo de análisis en más de 5 segundos.")
            .Validate(o => !o.Habilitado || !string.IsNullOrWhiteSpace(o.ApiKey),
                "AnalisisRecuerdos:ApiKey es obligatoria al habilitar el análisis de IA.")
            .ValidateOnStart();
        servicios.AddSingleton<ControlCuotaGemini>();
        servicios.AddHttpClient<AnalizadorRecuerdoGemini>(http => http.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        servicios.AddScoped<IAnalizadorRecuerdo>(sp =>
        {
            var opciones = sp.GetRequiredService<IOptions<OpcionesAnalisisRecuerdos>>().Value;
            return opciones.Proveedor.Trim().ToUpperInvariant() switch
            {
                "GEMINI" => sp.GetRequiredService<AnalizadorRecuerdoGemini>(),
                _ => throw new InvalidOperationException("El proveedor de análisis de IA no está implementado.")
            };
        });
        return servicios;
    }
}
