using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using ProyectoFinalTPI.Backend.Middlewares;
using ProyectoFinalTPI.Backend.Entidades;
namespace ProyectoFinalTPI.Backend.Tests;
public class RedisErrorHttpTests
{
    [Fact]
    public async Task QueUnaCaidaDelAlmacenDeSesionesDevuelva503SinExponerDetallesInternos()
    {
        //Arrange
        using var servicios = new ServiceCollection().BuildServiceProvider();
        using var cuerpo = new MemoryStream();
        var contexto = new DefaultHttpContext { RequestServices = servicios };
        contexto.Request.Path = "/api/feed/para-vos";
        contexto.Response.Body = cuerpo;
        var siguiente = new Mock<RequestDelegate>();
        siguiente.Setup(s => s(contexto)).ThrowsAsync(new AlmacenSesionNoDisponibleException(new Exception("detalle-interno-redis")));
        var logger = new Mock<ILogger<ManejoExcepcionesMiddleware>>();
        var middleware = new ManejoExcepcionesMiddleware(siguiente.Object, logger.Object);
        //Act
        await middleware.InvokeAsync(contexto);
        //Assert
        Assert.Equal(503, contexto.Response.StatusCode);
        Assert.StartsWith("application/problem+json", contexto.Response.ContentType);
        cuerpo.Position = 0;
        using var json = await JsonDocument.ParseAsync(cuerpo);
        Assert.Equal(503, json.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("detalle-interno-redis", json.RootElement.GetRawText());
        siguiente.Verify(s => s(contexto), Times.Once);
    }
}
