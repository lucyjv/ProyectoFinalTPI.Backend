using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.PuntosNostalgia;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class PuntosNostalgiaDtoTests
{
    private static readonly DateTimeOffset Reinicio = new(2026, 10, 2, 0, 0, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData(3, 15, 18)]
    [InlineData(0, 15, 15)]
    [InlineData(0, 0, 0)]
    public void QueElSaldoTotalSumeLosPuntosRestantesYExtra(int restantes, int extra, int total)
    {
        var saldo = new SaldoDiarioPuntosNostalgiaDto(5, restantes, Reinicio, extra);

        Assert.Equal(total, saldo.TotalDisponible);
    }

    [Fact]
    public void QueElSaldoSinPuntosCompradosUseCeroComoValorPorDefecto()
    {
        var saldo = new SaldoDiarioPuntosNostalgiaDto(5, 2, Reinicio);

        Assert.Equal(0, saldo.PuntosExtra);
        Assert.Equal(2, saldo.TotalDisponible);
    }

    [Theory]
    [InlineData("{}", false)]
    [InlineData("{\"packId\":null}", false)]
    [InlineData("{\"packId\":\"\"}", false)]
    [InlineData("{\"packId\":\"  \"}", false)]
    [InlineData("{\"packId\":\"ficha-retro\"}", true)]
    public void QueLaSolicitudDeCompraRequieraElIdentificadorDelPack(string json, bool esValida)
    {
        var solicitud = JsonSerializer.Deserialize<SolicitudCompraPackDto>(json, JsonSerializerOptions.Web);
        Assert.NotNull(solicitud);
        var errores = new List<ValidationResult>();

        var resultado = Validator.TryValidateObject(solicitud, new ValidationContext(solicitud), errores,
            validateAllProperties: true);

        Assert.Equal(esValida, resultado);
        if (esValida)
        {
            Assert.Equal("ficha-retro", solicitud.PackId);
            Assert.Empty(errores);
        }
        else
        {
            Assert.Equal(nameof(SolicitudCompraPackDto.PackId), Assert.Single(Assert.Single(errores).MemberNames));
        }
    }

    [Theory]
    [InlineData("{\"compraId\":42,\"paymentId\":\"0009007199254740993\"}", "0009007199254740993")]
    [InlineData("{\"compraId\":42}", null)]
    public void QueLaConfirmacionLeaElIdentificadorDePagoComoTexto(string json, string? paymentId)
    {
        var confirmacion = JsonSerializer.Deserialize<ConfirmacionCompraDto>(json, JsonSerializerOptions.Web);

        Assert.NotNull(confirmacion);
        Assert.Equal(42, confirmacion.CompraId);
        Assert.Equal(paymentId, confirmacion.PaymentId);
    }

    [Fact]
    public void QueLaCotizacionExpongaMonedaYUnImporteDecimalEnPesos()
    {
        var cotizacion = new CotizacionPuntosDto("USD", 1243.57m);

        var json = JsonSerializer.SerializeToElement(cotizacion, JsonSerializerOptions.Web);

        Assert.Equal("USD", json.GetProperty("moneda").GetString());
        Assert.Equal(1243.57m, json.GetProperty("cotizacionArs").GetDecimal());
    }

    [Fact]
    public void QueUnPackComunExpongaAmbosPreciosSinEtiquetaNiDestacado()
    {
        var pack = new PackPuntosDto("ficha-retro", "Ficha Retro", 15, 3m, 3731m, "rail");

        var json = JsonSerializer.SerializeToElement(pack, JsonSerializerOptions.Web);

        Assert.Equal("ficha-retro", json.GetProperty("id").GetString());
        Assert.Equal(15, json.GetProperty("puntos").GetInt32());
        Assert.Equal(3m, json.GetProperty("precioUsd").GetDecimal());
        Assert.Equal(3731m, json.GetProperty("precioArs").GetDecimal());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("etiqueta").ValueKind);
        Assert.False(json.GetProperty("destacado").GetBoolean());
    }

    [Fact]
    public void QueLaPreferenciaExpongaLosDatosParaContinuarElPago()
    {
        const string enlace = "https://checkout.example/abc-123";
        var preferencia = new PreferenciaCompraDto(42, "abc-123", enlace, "ficha-retro", "Ficha Retro",
            15, 3731m, 3m, 1243.57m);

        var json = JsonSerializer.SerializeToElement(preferencia, JsonSerializerOptions.Web);

        Assert.Equal(42, json.GetProperty("compraId").GetInt32());
        Assert.Equal("abc-123", json.GetProperty("preferenceId").GetString());
        Assert.Equal(enlace, json.GetProperty("initPoint").GetString());
        Assert.Equal("ficha-retro", json.GetProperty("packId").GetString());
        Assert.Equal(15, json.GetProperty("puntos").GetInt32());
        Assert.Equal(3731m, json.GetProperty("precioArs").GetDecimal());
        Assert.Equal(3m, json.GetProperty("precioUsd").GetDecimal());
        Assert.Equal(1243.57m, json.GetProperty("cotizacionDolar").GetDecimal());
    }

    [Fact]
    public void QueElResultadoDeCompraExpongaLaAcreditacionYElSaldoActual()
    {
        var resultado = new ResultadoCompraDto(42, "Aprobado", 15,
            new SaldoDiarioPuntosNostalgiaDto(5, 3, Reinicio, 20));

        var json = JsonSerializer.SerializeToElement(resultado, JsonSerializerOptions.Web);

        Assert.Equal(42, json.GetProperty("compraId").GetInt32());
        Assert.Equal("Aprobado", json.GetProperty("estado").GetString());
        Assert.Equal(15, json.GetProperty("puntosAcreditados").GetInt32());
        var saldo = json.GetProperty("saldoActual");
        Assert.Equal(5, saldo.GetProperty("cupo").GetInt32());
        Assert.Equal(3, saldo.GetProperty("puntosRestantes").GetInt32());
        Assert.Equal(20, saldo.GetProperty("puntosExtra").GetInt32());
        Assert.Equal(23, saldo.GetProperty("totalDisponible").GetInt32());
        Assert.True(Reinicio.EqualsExact(saldo.GetProperty("reinicio").GetDateTimeOffset()));
    }
}
