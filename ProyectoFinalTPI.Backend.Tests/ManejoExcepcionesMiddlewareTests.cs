using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ProyectoFinalTPI.Backend.Middlewares;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ManejoExcepcionesMiddlewareTests
{
    #region Manejo de Excepciones

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
