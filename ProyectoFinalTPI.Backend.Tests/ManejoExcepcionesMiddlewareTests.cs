using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ProyectoFinalTPI.Backend.Middlewares;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ManejoExcepcionesMiddlewareTests
{
    #region Manejo de Excepciones

    [Fact]
    public async Task QueRecuperacionNoDisponibleDevuelvaProblemDetailsCon503()
    {
        //Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/auth/recuperar-contrasena";
        context.TraceIdentifier = "recuperacion-test";
        using var body = new MemoryStream();
        context.Response.Body = body;
        var error = new RecuperacionContrasenaNoDisponibleException();
        var middleware = new ManejoExcepcionesMiddleware(_ => Task.FromException(error),
            NullLogger<ManejoExcepcionesMiddleware>.Instance);

        //Act
        await middleware.InvokeAsync(context);
        body.Position = 0;
        var problema = await JsonSerializer.DeserializeAsync<ProblemDetails>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        //Assert
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.NotNull(problema);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problema.Status);
        Assert.Equal("Recuperación de contraseña no disponible", problema.Title);
        Assert.Equal(error.Message, problema.Detail);
        Assert.Equal(context.Request.Path.Value, problema.Instance);
        Assert.Equal(context.TraceIdentifier, problema.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task QueSolicitudCanceladaPropagueExcepcionSinEscribirError()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancelacion.Token };
        using var body = new MemoryStream();
        context.Response.Body = body;
        var error = new OperationCanceledException(cancelacion.Token);
        var middleware = new ManejoExcepcionesMiddleware(_ => Task.FromException(error),
            NullLogger<ManejoExcepcionesMiddleware>.Instance);

        //Act
        var resultado = await Assert.ThrowsAsync<OperationCanceledException>(
            () => middleware.InvokeAsync(context));

        //Assert
        Assert.Same(error, resultado);
        Assert.Equal(0, body.Length);
    }

    #endregion
}
