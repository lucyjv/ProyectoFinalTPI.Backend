using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AnalisisRecuerdosServicio(ITrabajoAnalisisRepositorio repositorio, IAnalizadorRecuerdo analizador,
    IRelacionRecuerdosRepositorio relaciones, IOptions<OpcionesAnalisisRecuerdos> opciones,
    TimeProvider reloj, ILogger<AnalisisRecuerdosServicio> logger) : IAnalisisRecuerdosServicio
{
    private static readonly ActivitySource Fuente = new("Nostalgiar.AnalisisRecuerdos");
    private static readonly Meter Medidor = new("Nostalgiar.AnalisisRecuerdos");
    private static readonly Counter<long> Procesados = Medidor.CreateCounter<long>("analisis.trabajos.procesados");
    private static readonly Counter<long> Fallos = Medidor.CreateCounter<long>("analisis.trabajos.fallos");
    private static readonly Histogram<double> Duracion = Medidor.CreateHistogram<double>("analisis.duracion", "ms");

    #region Operaciones públicas

    public async Task<bool> ProcesarUno(CancellationToken ct)
    {
        var config = opciones.Value;
        var reserva = await ReservarTrabajo(config, ct);
        if (reserva == null) return false;
        return await ProcesarReserva(reserva, config, ct);
    }
    public async Task<EstadoAnalisisRecuerdoDto> ObtenerEstado(int id, CancellationToken ct)
    {
        var original = await ObtenerOriginalVisible(id, ct);
        return await repositorio.ObtenerEstado(original, ct)
            ?? throw new KeyNotFoundException(
                "La publicación no existe o no está disponible.");
    }

    #endregion

    #region Métodos privados y validaciones

    private Task<ReservaAnalisisDto?> ReservarTrabajo(
        OpcionesAnalisisRecuerdos config, CancellationToken ct) =>
        repositorio.Reservar(
            Ahora(),
            TimeSpan.FromSeconds(config.ReservaSegundos),
            config.MaxIntentos,
            ct);
    private async Task<bool> ProcesarReserva(
        ReservaAnalisisDto reserva,
        OpcionesAnalisisRecuerdos config,
        CancellationToken ct)
    {
        var inicio = Stopwatch.GetTimestamp();
        try
        {
            await ProcesarContenido(reserva, config, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            await RegistrarFallo(reserva, error, config, ct);
        }
        finally
        {
            Duracion.Record(
                Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
        }
        return true;
    }
    private async Task ProcesarContenido(
        ReservaAnalisisDto reserva,
        OpcionesAnalisisRecuerdos config,
        CancellationToken ct)
    {
        var entrada = await repositorio.ObtenerEntrada(
            reserva, Ahora(), ct);
        if (entrada == null)
        {
            await repositorio.Descartar(reserva, Ahora(), ct);
            return;
        }
        ValidarTamanioEntrada(entrada, config.MaxCaracteresEntrada);
        await AnalizarYGuardar(reserva, entrada, config, ct);
    }
    private async Task AnalizarYGuardar(
        ReservaAnalisisDto reserva,
        EntradaAnalisisRecuerdoDto entrada,
        OpcionesAnalisisRecuerdos config,
        CancellationToken ct)
    {
        using var tiempoMaximo = new CancellationTokenSource(
            TimeSpan.FromSeconds(config.TiempoMaximoSegundos), reloj);
        using var vinculada =
            CancellationTokenSource.CreateLinkedTokenSource(
                ct, tiempoMaximo.Token);

        var resultado = await analizador
            .Analizar(entrada, vinculada.Token)
            .WaitAsync(vinculada.Token);

        ValidarResultado(resultado);
        await repositorio.Completar(
            reserva, resultado, analizador.Version, Ahora(), ct);
        Procesados.Add(1);
    }
    private static void ValidarTamanioEntrada(
        EntradaAnalisisRecuerdoDto entrada, int maximo)
    {
        if (entrada.Titulo.Length + entrada.Descripcion.Length > maximo)
            throw new ArgumentException(
                "La entrada excede el límite del analizador.");
    }
    private void ValidarResultado(
        [NotNull] ResultadoAnalisisRecuerdoDto? resultado)
    {
        ValidadorPropuestasMomento.Validar(resultado?.Propuestas);
        if (resultado == null ||
            resultado.Momentos == null ||
            resultado.Momentos.Count > 50 ||
            (!resultado.AnalisisRealizado &&
                (resultado.Momentos.Count != 0 ||
                    resultado.Propuestas?.Count > 0)) ||
            resultado.Momentos.Any(m =>
                m == null ||
                m.MomentoId <= 0 ||
                !double.IsFinite(m.Confianza) ||
                m.Confianza < 0 ||
                m.Confianza > 1) ||
            resultado.Momentos.Select(m => m.MomentoId)
                .Distinct().Count() != resultado.Momentos.Count ||
            string.IsNullOrWhiteSpace(analizador.Version) ||
            analizador.Version.Length > 100)
            throw new ArgumentException(
                "El resultado del analizador no es válido.");
    }
    private async Task RegistrarFallo(
        ReservaAnalisisDto reserva,
        Exception error,
        OpcionesAnalisisRecuerdos config,
        CancellationToken ct)
    {
        var codigo = ObtenerCodigoError(error);
        var ahora = Ahora();
        var espera = CalcularEsperaReintento(
            reserva.Intentos, error, config);

        await repositorio.RegistrarFallo(
            reserva, codigo, ahora, ahora.AddSeconds(espera),
            ObtenerMaxIntentos(error, config.MaxIntentos), ct);

        Fallos.Add(1);
        // No registrar textos privados del proveedor ni del recuerdo.
        logger.LogWarning(
            "Falló el trabajo de análisis {TrabajoId}: {Codigo}",
            reserva.Id, codigo);
    }
    private static string ObtenerCodigoError(Exception error) =>
        (error as ErrorProveedorAnalisis)?.Codigo ?? error switch
        {
            ConflictoCatalogoException => "catalogo_ambiguo",
            OperationCanceledException => "tiempo_agotado",
            ArgumentException => "analisis_invalido",
            _ => "analisis_error"
        };
    private static double CalcularEsperaReintento(
        int intentos, Exception error, OpcionesAnalisisRecuerdos config)
    {
        var espera =
            Math.Min(
                300,
                config.ReintentoBaseSegundos *
                    Math.Pow(2, intentos - 1))
            * (0.9 + Random.Shared.NextDouble() * 0.2);

        return error is ErrorProveedorAnalisis { ReintentarEn: { } minimo }
            ? Math.Max(espera, minimo.TotalSeconds)
            : espera;
    }
    private static int ObtenerMaxIntentos(Exception error, int maximo) =>
        error is ArgumentException
            or ConflictoCatalogoException
            or ErrorProveedorAnalisis { Permanente: true }
            ? 1
            : maximo;
    private async Task<int> ObtenerOriginalVisible(
        int id, CancellationToken ct)
    {
        ValidarId(id);
        return await relaciones.ObtenerOriginalVisible(id, ct)
            ?? throw new KeyNotFoundException(
                "La publicación no existe o no está disponible.");
    }
    private static void ValidarId(int id)
    {
        if (id <= 0)
            throw new ArgumentException(
                "El identificador debe ser positivo.");
    }
    private DateTime Ahora() => reloj.GetUtcNow().UtcDateTime;

    #endregion
}
