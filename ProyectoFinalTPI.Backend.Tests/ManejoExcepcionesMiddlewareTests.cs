using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ProyectoFinalTPI.Backend.Middlewares;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ManejoExcepcionesMiddlewareTests
{
    [Fact]
    public async Task SolicitudCanceladaPropagaLaExcepcionSinEscribirUnError()
    {
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancelacion.Token };
        using var body = new MemoryStream();
        context.Response.Body = body;
        var error = new OperationCanceledException(cancelacion.Token);
        var middleware = new ManejoExcepcionesMiddleware(_ => Task.FromException(error),
            NullLogger<ManejoExcepcionesMiddleware>.Instance);

        var resultado = await Assert.ThrowsAsync<OperationCanceledException>(
            () => middleware.InvokeAsync(context));

        Assert.Same(error, resultado);
        Assert.Equal(0, body.Length);
    }
}
