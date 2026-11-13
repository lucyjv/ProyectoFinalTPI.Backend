using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.Diagnostics;

namespace ProyectoFinalTPI.Backend.Monitoreo;

public static class RegistroMonitoreo
{
    public static void Agregar(WebApplicationBuilder builder)
    {
        // lee la variable de Azure Monitoreo__Habilitado
        if (!builder.Configuration.GetValue<bool>("Monitoreo:Habilitado")) return;

        // busca la conexión al recurso de Application Insights
        var conexion = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(conexion))
            throw new InvalidOperationException("Monitoreo requiere APPLICATIONINSIGHTS_CONNECTION_STRING.");

        var proporcion = builder.Configuration.GetValue<float>("Monitoreo:ProporcionMuestreo", 0.2f);
        if (!float.IsFinite(proporcion) || proporcion <= 0 || proporcion > 1)
            throw new InvalidOperationException("Monitoreo:ProporcionMuestreo debe ser mayor que 0 y hasta 1.");

        //envío de datos
        builder.Services.AddOpenTelemetry().UseAzureMonitor(options => //conecta el monitoreo del backend con Azure
        {
            options.ConnectionString = conexion;
            options.TracesPerSecond = null;
            options.SamplingRatio = proporcion;
        })
        .WithTracing(traces => traces
            .AddSource("Nostalgiar.Feed", "Nostalgiar.AnalisisRecuerdos", "Nostalgiar.Redis", "Npgsql")
            .AddProcessor(new DatosOperativosProcessor()))
        .WithMetrics(metrics => metrics.AddMeter("Nostalgiar.Feed", "Nostalgiar.AnalisisRecuerdos", "Nostalgiar.Redis", "Nostalgiar.Auth"));
        // addSourse “Ademas de las peticiones y llamadas HTTP, escucha las mediciones de nuestro feed, analisis, Redis y PostgreSQL”

        // Evita enviar logs de consultas SQL y eventos masivos del framework
        builder.Logging.AddFilter<OpenTelemetry.Logs.OpenTelemetryLoggerProvider>(null, LogLevel.Warning);
    }
}

internal sealed class DatosOperativosProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity actividad)
    {
        foreach (var tag in actividad.TagObjects.ToArray())
        {
            if (tag.Key is "db.statement" or "db.query.text" or "url.query" ||
                tag.Key.StartsWith("db.parameters.", StringComparison.Ordinal))
                actividad.SetTag(tag.Key, null);
            else if (tag.Key is "url.full" or "http.url" && tag.Value is string valor &&
                Uri.TryCreate(valor, UriKind.Absolute, out var uri))
                actividad.SetTag(tag.Key, uri.GetLeftPart(UriPartial.Path));
        }
        if (actividad.Source.Name == "Npgsql") actividad.DisplayName = "PostgreSQL";
    }
}
