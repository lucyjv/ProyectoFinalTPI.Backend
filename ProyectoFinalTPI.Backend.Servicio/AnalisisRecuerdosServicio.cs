using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AnalisisRecuerdosServicio(ITrabajoAnalisisRepositorio repositorio, IAnalizadorRecuerdo analizador,
    IRelacionRecuerdosRepositorio relaciones, IOptions<OpcionesAnalisisRecuerdos> opciones,
    TimeProvider reloj, ILogger<AnalisisRecuerdosServicio> logger) : IAnalisisRecuerdosServicio
{
    private static readonly Meter Medidor = new("Nostalgiar.AnalisisRecuerdos");
    private static readonly Counter<long> Procesados = Medidor.CreateCounter<long>("analisis.trabajos.procesados");
    private static readonly Counter<long> Fallos = Medidor.CreateCounter<long>("analisis.trabajos.fallos");
    private static readonly Histogram<double> Duracion = Medidor.CreateHistogram<double>("analisis.duracion", "ms");

    public async Task<bool> ProcesarUno(CancellationToken ct)
    {
        var config = opciones.Value;
        var reserva = await repositorio.Reservar(Ahora(), TimeSpan.FromSeconds(config.ReservaSegundos), config.MaxIntentos, ct);
        if (reserva == null) return false;
        var inicio = Stopwatch.GetTimestamp();
        try
        {
            var entrada = await repositorio.ObtenerEntrada(reserva, Ahora(), ct);
            if (entrada == null)
            {
                await repositorio.Descartar(reserva, Ahora(), ct);
                return true;
            }
            if (entrada.Titulo.Length + entrada.Descripcion.Length > config.MaxCaracteresEntrada)
                throw new ArgumentException("La entrada excede el límite del analizador.");
            using var tiempoMaximo = new CancellationTokenSource(TimeSpan.FromSeconds(config.TiempoMaximoSegundos), reloj);
            using var vinculada = CancellationTokenSource.CreateLinkedTokenSource(ct, tiempoMaximo.Token);
            var resultado = await analizador.Analizar(entrada, vinculada.Token).WaitAsync(vinculada.Token);
            ValidadorPropuestasMomento.Validar(resultado?.Propuestas);
            if (resultado == null || resultado.Momentos == null || resultado.Momentos.Count > 50 ||
                (!resultado.AnalisisRealizado && (resultado.Momentos.Count != 0 || resultado.Propuestas?.Count > 0)) ||
                resultado.Momentos.Any(m => m == null || m.MomentoId <= 0 || !double.IsFinite(m.Confianza) || m.Confianza < 0 || m.Confianza > 1) ||
                resultado.Momentos.Select(m => m.MomentoId).Distinct().Count() != resultado.Momentos.Count ||
                string.IsNullOrWhiteSpace(analizador.Version) || analizador.Version.Length > 100)
                throw new ArgumentException("El resultado del analizador no es válido.");
            await repositorio.Completar(reserva, resultado, analizador.Version, Ahora(), ct);
            Procesados.Add(1);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            var proveedor = error as ErrorProveedorAnalisis;
            var codigo = proveedor?.Codigo ?? (error is ConflictoCatalogoException ? "catalogo_ambiguo" :
                error is OperationCanceledException ? "tiempo_agotado" : error is ArgumentException ? "analisis_invalido" : "analisis_error");
            var ahora = Ahora();
            var espera = Math.Min(300, config.ReintentoBaseSegundos * Math.Pow(2, reserva.Intentos - 1)) * (0.9 + Random.Shared.NextDouble() * 0.2);
            if (proveedor?.ReintentarEn is { } minimo) espera = Math.Max(espera, minimo.TotalSeconds);
            await repositorio.RegistrarFallo(reserva, codigo, ahora, ahora.AddSeconds(espera),
                error is ArgumentException or ConflictoCatalogoException || proveedor?.Permanente == true ? 1 : config.MaxIntentos, ct);
            Fallos.Add(1);
            // No guardar/loguear títulos, descripciones, prompts ni mensajes arbitrarios del proveedor.
            logger.LogWarning("Falló el trabajo de análisis {TrabajoId}: {Codigo}", reserva.Id, codigo);
        }
        finally { Duracion.Record(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds); }
        return true;
    }

    public async Task<EstadoAnalisisRecuerdoDto> ObtenerEstado(int id, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentException("El identificador debe ser positivo.");
        var original = await relaciones.ObtenerOriginalVisible(id, ct)
            ?? throw new KeyNotFoundException("La publicación no existe o no está disponible.");
        return await repositorio.ObtenerEstado(original, ct)
            ?? throw new KeyNotFoundException("La publicación no existe o no está disponible.");
    }

    private DateTime Ahora() => reloj.GetUtcNow().UtcDateTime;
}
