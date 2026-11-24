using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class CotizacionDolarServicioTests
{
    [Fact]
    public async Task UsaRespaldoAnteFallosYReutilizaLaCotizacionValida()
    {
        const string clave = "nostalgiar:economia:cotizacion_dolar";
        var cache = new Mock<IDistributedCache>();
        cache.SetupSequence(c => c.GetAsync(clave, default))
            .ThrowsAsync(new InvalidOperationException("Redis no disponible"))
            .ReturnsAsync(Encoding.UTF8.GetBytes("invalida"))
            .ReturnsAsync((byte[]?)null)
            .ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(clave, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default))
            .ThrowsAsync(new InvalidOperationException("Redis no disponible"));
        using var proveedor = new ProveedorCotizacion(
            () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            () => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { venta = -1m }) },
            () => throw new HttpRequestException("Proveedor no disponible"),
            () => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { venta = 1300.25m }) });
        var clientes = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        clientes.Setup(c => c.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(proveedor, false));
        var opciones = new EconomiaOptions { DolarApiUrl = "https://cotizaciones.example/actual", CotizacionDolarFallback = 0, CacheMinutos = 1 };
        var servicio = new CotizacionDolarServicio(clientes.Object, Options.Create(opciones), cache.Object,
            NullLogger<CotizacionDolarServicio>.Instance);

        Assert.Equal(1200m, await servicio.ObtenerCotizacionAsync());
        opciones.CotizacionDolarFallback = 1250m;
        Assert.Equal(1250m, await servicio.ObtenerCotizacionAsync());
        Assert.Equal(1250m, await servicio.ObtenerCotizacionAsync());
        Assert.Equal(1300.25m, await servicio.ObtenerCotizacionAsync());
        Assert.Equal(1300.25m, await servicio.ObtenerCotizacionAsync());
        opciones.CotizacionDolarFallback = 1400m;
        Assert.Equal(1400m, await servicio.ObtenerCotizacionAsync());

        Assert.Equal(4, proveedor.Consultas.Count);
        Assert.All(proveedor.Consultas, consulta => Assert.Equal(new Uri(opciones.DolarApiUrl), consulta));
        cache.Verify(c => c.GetAsync(clave, default), Times.Exactly(4));
        cache.Verify(c => c.SetAsync(clave,
            It.Is<byte[]>(datos => Encoding.UTF8.GetString(datos) == "1300.25"),
            It.Is<DistributedCacheEntryOptions>(opciones => opciones.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(5)),
            default), Times.Once);
    }

    private sealed class ProveedorCotizacion(params Func<HttpResponseMessage>[] respuestas) : HttpMessageHandler
    {
        public List<Uri> Consultas { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Consultas.Add(request.RequestUri!);
            return Task.FromResult(respuestas[Consultas.Count - 1]());
        }
    }
}
