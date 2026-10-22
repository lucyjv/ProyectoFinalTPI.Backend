using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Middlewares;

/// <summary>Contrato común para las excepciones no manejadas de la aplicación.</summary>
public sealed class ManejoExcepcionesMiddleware(
    RequestDelegate next, ILogger<ManejoExcepcionesMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (estado, titulo, detalle) = exception switch
            {
                ConflictoCatalogoException => (409, "Conflicto de catálogo", exception.Message),
                AnalisisNoDisponibleException => (503, "Análisis no disponible", exception.Message),
                ConflictoRelacionesException => (409, "Conflicto de relaciones", exception.Message),
                ArgumentException => (400, "Solicitud inválida", exception.Message),
                UnauthorizedAccessException => (403, "Acceso denegado", exception.Message),
                KeyNotFoundException => (404, "Recurso no encontrado", exception.Message),
                _ => (500, "Error interno del servidor",
                    "No se pudo completar la operación. Intentá nuevamente más tarde.")
            };

            if (estado == 500)
            {
                logger.LogError(exception,
                    "Error al procesar {Metodo} {Ruta}. TraceId: {TraceId}",
                    context.Request.Method, context.Request.Path, context.TraceIdentifier);
            }

            var problema = new ProblemDetails
            {
                Status = estado,
                Title = titulo,
                Detail = detalle,
                Instance = context.Request.Path
            };
            problema.Extensions["traceId"] = context.TraceIdentifier;

            context.Response.Clear();
            context.Response.StatusCode = estado;
            await context.Response.WriteAsJsonAsync(problema,
                options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
        }
    }
}

