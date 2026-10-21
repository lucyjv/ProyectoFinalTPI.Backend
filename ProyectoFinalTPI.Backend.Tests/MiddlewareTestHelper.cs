using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ProyectoFinalTPI.Backend.Middlewares;

namespace ProyectoFinalTPI.Backend.Tests;

internal static class MiddlewareTestHelper
{
    public static async Task<IActionResult> Ejecutar(HttpContext context, Func<Task<IActionResult?>> accion)
    {
        using var body = new MemoryStream();
        context.Response.Body = body;
        IActionResult? resultado = null;
        var middleware = new ManejoExcepcionesMiddleware(async _ => resultado = await accion(),
            NullLogger<ManejoExcepcionesMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        if (resultado != null) return resultado;

        body.Position = 0;
        var problema = await JsonSerializer.DeserializeAsync<ProblemDetails>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var respuesta = new ObjectResult(problema) { StatusCode = context.Response.StatusCode };
        respuesta.ContentTypes.Add(context.Response.ContentType!);
        return respuesta;
    }
}
