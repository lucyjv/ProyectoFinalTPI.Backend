using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ProyectoFinalTPI.Backend.Servicio;


internal sealed class TelemetriaFeed : IDisposable
{
    private static readonly ActivitySource Fuente = new("Nostalgiar.Feed");
    private static readonly Meter Medidor = new("Nostalgiar.Feed");
    private static readonly Histogram<double> Duracion = Medidor.CreateHistogram<double>("feed.etapa.duracion", "ms");
    private static readonly Histogram<long> Cantidad = Medidor.CreateHistogram<long>("feed.elementos", "{elemento}");
    public static void RegistrarCantidad(string etapa, long cantidad) => Cantidad.Record(cantidad,
        new KeyValuePair<string, object?>("etapa", etapa));
    private readonly string etapa;
    private readonly long inicio = Stopwatch.GetTimestamp();
    private readonly Activity? actividad;
    private string resultado = "error";

    public TelemetriaFeed(string etapa)
    {
        this.etapa = etapa;
        actividad = Fuente.StartActivity($"feed.{etapa}");
    }
    public void Completar() => resultado = "ok";
    public void Cancelar() => resultado = "cancelado";
    public void Dispose()
    {
        actividad?.SetTag("resultado", resultado);
        if (resultado == "error") actividad?.SetStatus(ActivityStatusCode.Error);
        Duracion.Record(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds, //cuando termina, calcula y registra el tiempo
            new KeyValuePair<string, object?>("etapa", etapa),
            new KeyValuePair<string, object?>("resultado", resultado));
        actividad?.Dispose();
    }
}
