using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.PuntosNostalgia;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class PuntosNostalgiaControllerTests
{
    private const int UsuarioId = 21;
    private static readonly SaldoDiarioPuntosNostalgiaDto Saldo = new(5, 3, DateTimeOffset.UnixEpoch, 15);
    private static readonly ResultadoCompraDto Aprobada = new(42, "Aprobado", 15, Saldo);
    private readonly Mock<ICupoPuntosNostalgiaServicio> _cupos = new(MockBehavior.Strict);
    private readonly Mock<IMercadoPagoPuntosServicio> _pagos = new(MockBehavior.Strict);
    private readonly Mock<ICotizacionDolarServicio> _cotizacion = new(MockBehavior.Strict);

    [Fact]
    public async Task ConsultarSaldoUsaElUsuarioDeLaSesion()
    {
        _cupos.Setup(s => s.ObtenerSaldo(UsuarioId, default)).ReturnsAsync(Saldo);

        var respuesta = await CrearController().ObtenerSaldo(default);

        Assert.Equal(Saldo, Assert.IsType<SaldoDiarioPuntosNostalgiaDto>(
            Assert.IsType<OkObjectResult>(respuesta.Result).Value));
        _pagos.VerifyNoOtherCalls();
        _cotizacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConsultarPacksConservaLosPreciosDelCatalogo()
    {
        PackPuntosDto[] packs = [new("ficha-retro", "Ficha Retro", 15, 3m, 3750m, "rail")];
        _pagos.Setup(s => s.ObtenerPacksDisponiblesAsync(default)).ReturnsAsync(packs);

        var respuesta = await CrearController().ObtenerPacks(default);

        Assert.Same(packs, Assert.IsType<OkObjectResult>(respuesta.Result).Value);
    }

    [Fact]
    public async Task ConsultarCotizacionDevuelveDolaresYElImporteEnPesos()
    {
        _cotizacion.Setup(s => s.ObtenerCotizacionAsync(default)).ReturnsAsync(1250.25m);

        var respuesta = await CrearController().ObtenerCotizacion(default);

        Assert.Equal(new CotizacionPuntosDto("USD", 1250.25m),
            Assert.IsType<CotizacionPuntosDto>(Assert.IsType<OkObjectResult>(respuesta.Result).Value));
    }

    [Fact]
    public async Task ComprarUsaElPackSolicitadoYElUsuarioDeLaSesion()
    {
        var compra = new PreferenciaCompraDto(42, "pref-42", "https://checkout.example/pago",
            "ficha-retro", "Ficha Retro", 15, 3750m, 3m, 1250m);
        _pagos.Setup(s => s.CrearPreferenciaCompraAsync(UsuarioId, "ficha-retro", default)).ReturnsAsync(compra);

        var respuesta = await CrearController().CrearPreferencia(new() { PackId = "ficha-retro" }, default);

        Assert.Same(compra, Assert.IsType<OkObjectResult>(respuesta.Result).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task ConfirmarSinIdentificadorRechazaLaCompra(string? id)
    {
        var respuesta = await CrearController().ConfirmarCompra(new() { PaymentId = id }, default);

        var problema = Assert.IsType<ProblemDetails>(Assert.IsType<BadRequestObjectResult>(respuesta.Result).Value);
        Assert.Equal("Pago no verificado", problema.Title);
        _pagos.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfirmarDevuelveSegunLoQueVerificoElProveedor(bool aprobado)
    {
        _pagos.Setup(s => s.ProcesarNotificacionPagoAsync("12345", default))
            .ReturnsAsync(aprobado ? Aprobada : null);

        var respuesta = await CrearController().ConfirmarCompra(new() { PaymentId = "12345" }, default);

        if (aprobado)
        {
            Assert.Same(Aprobada, Assert.IsType<OkObjectResult>(respuesta.Result).Value);
        }
        else
        {
            var problema = Assert.IsType<ProblemDetails>(Assert.IsType<BadRequestObjectResult>(respuesta.Result).Value);
            Assert.Equal("Pago no aprobado", problema.Title);
        }
    }

    [Theory]
    [InlineData("12345", "ignorado", null, "12345", true)]
    [InlineData(null, "12345", null, "12345", false)]
    [InlineData(null, null, "{\"data\":{\"id\":\"12345\"}}", "12345", true)]
    [InlineData(null, null, "{\"id\":\"12345\"}", "12345", true)]
    [InlineData(null, null, "{}", null, false)]
    public async Task WebhookUsaElIdentificadorDeLaConsultaOElCuerpo(
        string? id, string? dataId, string? cuerpo, string? esperado, bool aprobado)
    {
        if (esperado is not null)
        {
            _pagos.Setup(s => s.ProcesarNotificacionPagoAsync(esperado, default))
                .ReturnsAsync(aprobado ? Aprobada : null);
        }
        using var documento = cuerpo is null ? null : JsonDocument.Parse(cuerpo);

        var respuesta = await CrearController().WebhookMercadoPago(null, id, dataId, documento?.RootElement, default);

        var json = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(respuesta).Value);
        Assert.Equal(aprobado ? "approved" : "received", json.GetProperty("status").GetString());
        if (aprobado)
        {
            Assert.Equal(Aprobada.CompraId, json.GetProperty("compraId").GetInt32());
            Assert.Equal(Aprobada.PuntosAcreditados, json.GetProperty("puntos").GetInt32());
        }
        if (esperado is null)
            _pagos.VerifyNoOtherCalls();
        else
            _pagos.Verify(s => s.ProcesarNotificacionPagoAsync(esperado, default), Times.Once);
    }

    private PuntosNostalgiaController CrearController() => new(_cupos.Object, _pagos.Object, _cotizacion.Object)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "21")], "test"))
            }
        }
    };
}
