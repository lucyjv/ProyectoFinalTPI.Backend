using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Infraestructura;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class MercadoPagoPuntosServicioTests
{
    private const int CompraId = 42;
    private const int UsuarioId = 21;
    private const int Puntos = 15;
    private const string PagoId = "12345";
    private const string UrlPago = "https://api.mercadopago.com/v1/payments/" + PagoId;

    [Fact]
    public async Task ElCatalogoCalculaLosImportesEnPesosYMarcaElPackDestacado()
    {
        await using var contexto = await CrearContextoConCompraPendiente();
        var cotizacion = new Mock<ICotizacionDolarServicio>();
        cotizacion.Setup(c => c.ObtenerCotizacionAsync(default)).ReturnsAsync(1250.25m);
        var servicio = new MercadoPagoPuntosServicio(contexto, cotizacion.Object,
            Mock.Of<IConsumoDiarioPuntosNostalgiaRepositorio>(), Mock.Of<ICupoPuntosNostalgiaServicio>(),
            Mock.Of<IMercadoPagoCliente>());

        var packs = await servicio.ObtenerPacksDisponiblesAsync();

        Assert.Collection(packs,
            ficha => Assert.Equal(("ficha-retro", 15, 3751m, false), (ficha.Id, ficha.Puntos, ficha.PrecioArs, ficha.Destacado)),
            cassette => Assert.Equal(("cassette-nostalgico", 45, 8752m, true), (cassette.Id, cassette.Puntos, cassette.PrecioArs, cassette.Destacado)),
            vinilo => Assert.Equal(("vinilo-coleccion", 100, 15003m, false), (vinilo.Id, vinilo.Puntos, vinilo.PrecioArs, vinilo.Destacado)));
        cotizacion.Verify(c => c.ObtenerCotizacionAsync(default), Times.Once);
    }

    [Fact]
    public async Task UnPackInexistenteNoGeneraUnaCompraNiContactoConElProveedor()
    {
        await using var contexto = await CrearContextoConCompraPendiente();
        var cotizacion = new Mock<ICotizacionDolarServicio>();
        var cliente = new Mock<IMercadoPagoCliente>();
        var servicio = new MercadoPagoPuntosServicio(contexto, cotizacion.Object,
            Mock.Of<IConsumoDiarioPuntosNostalgiaRepositorio>(), Mock.Of<ICupoPuntosNostalgiaServicio>(), cliente.Object);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => servicio.CrearPreferenciaCompraAsync(UsuarioId, "inexistente"));

        Assert.Equal("packId", error.ParamName);
        Assert.Equal(CompraId, (await contexto.ComprasPuntosNostalgia.AsNoTracking().SingleAsync()).Id);
        cotizacion.VerifyNoOtherCalls();
        cliente.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CrearCompraGuardaLaPreferenciaDelClienteYConservaElPrecioCalculado()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var contexto = new ApplicationDbContext(opciones);
        var cotizacion = new Mock<ICotizacionDolarServicio>();
        cotizacion.Setup(c => c.ObtenerCotizacionAsync(default)).ReturnsAsync(1000m);
        var cliente = new Mock<IMercadoPagoCliente>();
        cliente.Setup(c => c.CrearPreferenciaAsync(It.IsAny<SolicitudPreferenciaMercadoPago>(), default))
            .ReturnsAsync(new PreferenciaMercadoPago("pref-42", "https://checkout.example/pago"));
        var consumos = new Mock<IConsumoDiarioPuntosNostalgiaRepositorio>();
        var servicio = new MercadoPagoPuntosServicio(contexto, cotizacion.Object, consumos.Object,
            Mock.Of<ICupoPuntosNostalgiaServicio>(), cliente.Object);

        var resultado = await servicio.CrearPreferenciaCompraAsync(UsuarioId, "ficha-retro");

        var compra = await contexto.ComprasPuntosNostalgia.AsNoTracking().SingleAsync();
        Assert.Equal(compra.Id, resultado.CompraId);
        Assert.Equal("pref-42", compra.PreferenceId);
        Assert.Equal(compra.PreferenceId, resultado.PreferenceId);
        Assert.Equal("https://checkout.example/pago", resultado.InitPoint);
        Assert.Equal("Pendiente", compra.Estado);
        Assert.Equal(UsuarioId, compra.UsuarioId);
        Assert.Equal(3000m, compra.PrecioArs);
        Assert.Equal(compra.PrecioArs, resultado.PrecioArs);
        cliente.Verify(c => c.CrearPreferenciaAsync(
            new SolicitudPreferenciaMercadoPago(compra.Id, "ficha-retro", "Ficha Retro", Puntos, 3000m), default), Times.Once);
        consumos.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ErrorDeConexionNoAcreditaNiModificaLaCompra()
    {
        await using var contexto = await CrearContextoConCompraPendiente();
        var cliente = new Mock<IMercadoPagoCliente>();
        cliente.Setup(c => c.ConsultarPagoAsync(PagoId, default)).ThrowsAsync(new HttpRequestException("Sin conexión"));
        var consumos = new Mock<IConsumoDiarioPuntosNostalgiaRepositorio>();
        var servicio = new MercadoPagoPuntosServicio(contexto, Mock.Of<ICotizacionDolarServicio>(), consumos.Object,
            Mock.Of<ICupoPuntosNostalgiaServicio>(), cliente.Object);

        Assert.Null(await servicio.ProcesarNotificacionPagoAsync(PagoId));
        var compra = await contexto.ComprasPuntosNostalgia.AsNoTracking().SingleAsync();
        Assert.Equal("Pendiente", compra.Estado);
        Assert.Null(compra.FechaAcreditacion);
        consumos.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueUnaNotificacionAprobadaRepetidaAcrediteUnaSolaVez()
    {
        await using var contexto = await CrearContextoConCompraPendiente();
        using var handler = new RespuestaPago("approved", CompraId.ToString());
        using var cliente = new HttpClient(handler, disposeHandler: false);
        var consumos = new Mock<IConsumoDiarioPuntosNostalgiaRepositorio>();
        var cupos = new Mock<ICupoPuntosNostalgiaServicio>();
        var saldo = new SaldoDiarioPuntosNostalgiaDto(5, 3, DateTimeOffset.UtcNow.AddDays(1), Puntos);
        cupos.Setup(s => s.ObtenerSaldo(UsuarioId, default)).ReturnsAsync(saldo);
        var servicio = CrearServicio(contexto, cliente, consumos.Object, cupos.Object);

        var primeraConfirmacion = await servicio.ProcesarNotificacionPagoAsync(PagoId);

        Assert.NotNull(primeraConfirmacion);
        Assert.Equal(CompraId, primeraConfirmacion.CompraId);
        Assert.Equal(Puntos, primeraConfirmacion.PuntosAcreditados);
        var compraAprobada = await contexto.ComprasPuntosNostalgia.AsNoTracking().SingleAsync();
        Assert.Equal("Aprobado", compraAprobada.Estado);
        Assert.Equal(PagoId, compraAprobada.PaymentId);
        Assert.NotNull(compraAprobada.FechaAcreditacion);

        contexto.ChangeTracker.Clear();
        var segundaConfirmacion = await servicio.ProcesarNotificacionPagoAsync(PagoId);

        Assert.Equal(primeraConfirmacion, segundaConfirmacion);
        var compraReconsultada = await contexto.ComprasPuntosNostalgia.AsNoTracking().SingleAsync();
        Assert.Equal(compraAprobada.FechaAcreditacion, compraReconsultada.FechaAcreditacion);
        Assert.Equal(new[] { (HttpMethod.Get, UrlPago), (HttpMethod.Get, UrlPago) }, handler.Consultas);
        consumos.Verify(r => r.AcreditarPuntosExtra(UsuarioId, Puntos, default), Times.Once);
        consumos.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("pending", "42")]
    [InlineData("rejected", "42")]
    [InlineData("approved", null)]
    [InlineData("approved", "invalida")]
    [InlineData("approved", "99")]
    public async Task QueSoloSeAcrediteUnPagoAprobadoConCompraValida(string estado, string? referencia)
    {
        await using var contexto = await CrearContextoConCompraPendiente();
        using var handler = new RespuestaPago(estado, referencia);
        using var cliente = new HttpClient(handler, disposeHandler: false);
        var consumos = new Mock<IConsumoDiarioPuntosNostalgiaRepositorio>();
        var servicio = CrearServicio(contexto, cliente, consumos.Object, Mock.Of<ICupoPuntosNostalgiaServicio>());

        var resultado = await servicio.ProcesarNotificacionPagoAsync(PagoId);

        Assert.Equal((HttpMethod.Get, UrlPago), Assert.Single(handler.Consultas));
        Assert.Null(resultado);
        var compra = await contexto.ComprasPuntosNostalgia.AsNoTracking().SingleAsync();
        Assert.Equal("Pendiente", compra.Estado);
        Assert.Null(compra.PaymentId);
        Assert.Null(compra.FechaAcreditacion);
        consumos.VerifyNoOtherCalls();
    }

    private static async Task<ApplicationDbContext> CrearContextoConCompraPendiente()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var contexto = new ApplicationDbContext(opciones);
        contexto.ComprasPuntosNostalgia.Add(new CompraPuntosNostalgia
        {
            Id = CompraId,
            UsuarioId = UsuarioId,
            PackId = "ficha-retro",
            Puntos = Puntos
        });
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        return contexto;
    }

    private static MercadoPagoPuntosServicio CrearServicio(
        ApplicationDbContext contexto, HttpClient cliente,
        IConsumoDiarioPuntosNostalgiaRepositorio consumos, ICupoPuntosNostalgiaServicio cupos)
    {
        var mercadoPago = new MercadoPagoCliente(cliente,
            Options.Create(new MercadoPagoOptions { AccessToken = "token-de-prueba" }));
        return new MercadoPagoPuntosServicio(contexto, Mock.Of<ICotizacionDolarServicio>(), consumos, cupos,
            mercadoPago);
    }

    private sealed class RespuestaPago(string estado, string? referencia) : HttpMessageHandler
    {
        public List<(HttpMethod Metodo, string Url)> Consultas { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Consultas.Add((request.Method, request.RequestUri!.AbsoluteUri));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { status = estado, external_reference = referencia })
            });
        }
    }
}
