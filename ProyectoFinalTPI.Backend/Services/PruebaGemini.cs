using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;

namespace ProyectoFinalTPI.Backend.Services;

public static class PruebaGemini
{
    public static async Task Ejecutar(IConfiguration configuracion)
    {
        var opciones = configuracion.GetSection("Gemini").Get<OpcionesGemini>() ?? new();
        Validator.ValidateObject(opciones, new ValidationContext(opciones), validateAllProperties: true);
        if (string.IsNullOrWhiteSpace(opciones.ApiKey))
            throw new InvalidOperationException("Falta configurar Gemini:ApiKey en User Secrets.");
        opciones.Habilitado = true;
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        using var cuota = new ControlCuotaGemini(TimeProvider.System);
        var analizador = new AnalizadorRecuerdoGemini(http, new CatalogoVacio(), Options.Create(opciones), cuota);
        EntradaAnalisisRecuerdoDto[] ejemplos =
        [
            new(1, 1, "Argentina campeón del mundo 1986", "Festejamos el triunfo de Argentina en el Mundial de México 86.", new DateTime(1986, 6, 29)),
            new(2, 1, "Una tarde en la plaza", "Paseamos con mi familia y tomamos unos mates.", new DateTime(2000, 4, 20)),
            new(3, 1, "Argentina campeón del mundo 2022", "Los festejos por Argentina campeón del mundo en Qatar 2022.", new DateTime(2022, 12, 18))
        ];
        foreach (var ejemplo in ejemplos)
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try
            {
                var resultado = await analizador.Analizar(ejemplo, limite.Token);
                Console.WriteLine(JsonSerializer.Serialize(new { caso = ejemplo.PublicacionId, analizador.Version, resultado.Propuestas }));
            }
            catch (ErrorProveedorAnalisis e)
            {
                Console.Error.WriteLine($"Caso {ejemplo.PublicacionId}: {e.Codigo}. No se reintenta ni se cambia a otro modelo.");
                Environment.ExitCode = 1;
                return;
            }
        }
    }

    private sealed class CatalogoVacio : ICatalogoAnalisisRepositorio
    {
        public Task<IReadOnlyList<CandidatoMomentoDto>> BuscarCandidatos(string texto, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CandidatoMomentoDto>>([]);
    }
}
