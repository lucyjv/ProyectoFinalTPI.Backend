using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class JuegoTesoroServicioTests
{
    private readonly Mock<IJuegoTesoroRepositorio> repositorio = new(MockBehavior.Strict);
    private readonly Mock<IUsuarioRepositorio> usuarios = new(MockBehavior.Strict);
    private readonly RelojTesoro reloj = new();
    private readonly JuegoTesoroOpciones opciones = new()
    {
        Ubicaciones = new() { new() { Clave = "retiro", Pista = "Un Libertador a caballo guarda un secreto.",
            NombreHallazgo = "Plaza San Martín", Latitud = -34.59558, Longitud = -58.3785, ZoomMinimo = 18 } }
    };
    private JuegoTesoroServicio Servicio => new(repositorio.Object, usuarios.Object, Options.Create(opciones), reloj);
    private static VistaMapaTesoroDto Vista(double zoom = 18) => new()
    { Zoom = zoom, MinLatitud = -34.596, MaxLatitud = -34.595, MinLongitud = -58.379, MaxLongitud = -58.378 };

    public JuegoTesoroServicioTests()
    {
        usuarios.Setup(r => r.ObtenerPorIdAsync(7)).ReturnsAsync(new Usuario_Personal { Id = 7 });
        repositorio.Setup(r => r.ObtenerOCrearSemana(It.IsAny<RetoTesoro>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetoTesoro candidato, CancellationToken _) => { candidato.Id = 42; return candidato; });
        repositorio.Setup(r => r.ObtenerHallazgo(42, 7, It.IsAny<CancellationToken>())).ReturnsAsync((HallazgoTesoro?)null);
    }

    [Fact]
    public async Task QueLaSemanaCambieElDomingoALasCeroDeArgentina()
    {
        reloj.Ahora = DateTimeOffset.Parse("2026-11-29T02:59:59Z");
        var antes = (await Servicio.ObtenerActual(7))!;
        Assert.Equal(DateTime.Parse("2026-11-22T03:00:00Z").ToUniversalTime(), antes.InicioUtc);
        Assert.Equal(DateTime.Parse("2026-11-29T03:00:00Z").ToUniversalTime(), antes.FinUtc);
        reloj.Ahora = reloj.Ahora.AddSeconds(1);
        var despues = (await Servicio.ObtenerActual(7))!;
        Assert.Equal(antes.FinUtc, despues.InicioUtc);
    }

    [Theory]
    [InlineData(17.999)]
    [InlineData(0)]
    public async Task QueNoReveleElTesoroNiGuardeHallazgosConZoomInsuficiente(double zoom)
    {
        Assert.Null(await Servicio.ObtenerVisible(7, Vista(zoom)));
        await Assert.ThrowsAsync<ArgumentException>(() => Servicio.Recoger(7, 42, Vista(zoom)));
        repositorio.Verify(r => r.GuardarHallazgo(It.IsAny<HallazgoTesoro>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueMirarOtraZonaNoReveleNiPermitaRecogerElTesoro()
    {
        var vista = Vista();
        vista.MinLongitud = -60;
        vista.MaxLongitud = -59;
        Assert.Null(await Servicio.ObtenerVisible(7, vista));
        await Assert.ThrowsAsync<ArgumentException>(() => Servicio.Recoger(7, 42, vista));
    }

    [Fact]
    public async Task QueSePuedaVerYRecogerSinEnviarInclinacionNiGps()
    {
        var visible = (await Servicio.ObtenerVisible(7, Vista()))!;
        Assert.Equal(42, visible.RetoId);
        repositorio.Setup(r => r.GuardarHallazgo(It.IsAny<HallazgoTesoro>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HallazgoTesoro h, CancellationToken _) => h);
        var hallazgo = await Servicio.Recoger(7, visible.RetoId, Vista());
        Assert.Equal("Plaza San Martín", hallazgo.Nombre);
        Assert.Equal(reloj.GetUtcNow().UtcDateTime, hallazgo.HalladoUtc);
        repositorio.Verify(r => r.GuardarHallazgo(It.Is<HallazgoTesoro>(h => h.UsuarioId == 7 && h.RetoId == 42), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueUnHallazgoPrevioQuiteElTesoroDelMapa()
    {
        repositorio.Setup(r => r.ObtenerHallazgo(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HallazgoTesoro { RetoId = 42, UsuarioId = 7, FechaHallazgoUtc = reloj.GetUtcNow().UtcDateTime });
        Assert.NotNull((await Servicio.ObtenerActual(7))!.HalladoUtc);
        Assert.Null(await Servicio.ObtenerVisible(7, Vista()));
    }

    [Fact]
    public async Task QueRechaceUnaCapturaDeOtroReto()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Servicio.Recoger(7, 41, Vista()));
        repositorio.Verify(r => r.GuardarHallazgo(It.IsAny<HallazgoTesoro>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueRechaceUnaVistaConNumerosNoFinitosAntesDeConsultarDatos()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Servicio.ObtenerVisible(7, Vista(double.NaN)));
        usuarios.Verify(r => r.ObtenerPorIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task QueUnaCuentaSuspendidaNoParticipe()
    {
        usuarios.Setup(r => r.ObtenerPorIdAsync(7)).ReturnsAsync(new Usuario_Personal
        { Id = 7, SuspendidoHasta = reloj.GetUtcNow().UtcDateTime.AddDays(1) });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Servicio.ObtenerActual(7));
        repositorio.Verify(r => r.ObtenerOCrearSemana(It.IsAny<RetoTesoro>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueDeshabilitarElJuegoNoCreeRetos()
    {
        opciones.Habilitado = false;
        Assert.Null(await Servicio.ObtenerActual(7));
        repositorio.Verify(r => r.ObtenerOCrearSemana(It.IsAny<RetoTesoro>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class RelojTesoro : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = DateTimeOffset.Parse("2026-11-23T15:00:00Z");
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
