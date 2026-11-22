using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class RankingMusicaServicioTests
{
    [Theory]
    [InlineData(ReputacionEnum.Nostalgico, 5)]
    [InlineData(ReputacionEnum.Aedo, 7)]
    [InlineData(ReputacionEnum.Mnnemónide, 10)]
    [InlineData(null, 10)]
    public async Task QueVotarCancionUseElPermisoExistenteYElCupoDeLaCategoria(ReputacionEnum? reputacion, int cupo)
    {
        // Arrange
        Usuario usuario = reputacion is { } nivel ? new Usuario_Personal { Reputacion = nivel } : new Usuario_Marca();
        var progreso = new Mock<ICategoriaRepositorio>(MockBehavior.Strict);
        progreso.Setup(repo => repo.ObtenerUsuario(21, default)).ReturnsAsync(usuario);
        var categorias = new CategoriaServicio(progreso.Object);
        var consumo = new Mock<IConsumoDiarioPuntosNostalgiaRepositorio>(MockBehavior.Strict);
        var reloj = new Mock<TimeProvider>(MockBehavior.Strict);
        var puntos = new CupoPuntosNostalgiaServicio(categorias, consumo.Object, reloj.Object);
        var ranking = new Mock<IRankingMusicaRepositorio>(MockBehavior.Strict);
        ranking.Setup(repo => repo.RegistrarVoto(3, 21, 2, cupo, default))
            .ReturnsAsync(new VotoRankingMusicaRegistrado(42, 8, cupo - 2));
        var servicio = new RankingMusicaServicio(ranking.Object, categorias, puntos, reloj.Object, Options.Create(new RankingMusicaOptions()));

        // Act
        var voto = await servicio.PuntuarMusica(3, 21, 2);

        // Assert
        Assert.Equal(42, voto.Id);
        Assert.Equal(8, voto.PuntosTotales);
        Assert.Equal(cupo - 2, voto.PuntosDiariosRestantes);
        ranking.Verify(repo => repo.RegistrarVoto(3, 21, 2, cupo, default), Times.Once);
        consumo.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ReputacionEnum.Nostalgico)]
    [InlineData(ReputacionEnum.Aedo)]
    public async Task QueUnaCategoriaSinSugerirMusicaNoPuedaCrearCandidaturas(ReputacionEnum reputacion)
    {
        // Arrange
        var progreso = new Mock<ICategoriaRepositorio>(MockBehavior.Strict);
        progreso.Setup(repo => repo.ObtenerUsuario(21, default)).ReturnsAsync(new Usuario_Personal { Reputacion = reputacion });
        var repositorio = new Mock<IRankingMusicaRepositorio>(MockBehavior.Strict);
        var servicio = new RankingMusicaServicio(repositorio.Object, new CategoriaServicio(progreso.Object),
            Mock.Of<ICupoPuntosNostalgiaServicio>(), Mock.Of<TimeProvider>(), Options.Create(new RankingMusicaOptions()));

        // Act
        var error = await Record.ExceptionAsync(() => servicio.SugerirMusica(21, 1980, "Canción", "Banda"));

        // Assert
        Assert.IsType<UnauthorizedAccessException>(error);
        repositorio.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(15)]
    [InlineData(7)]
    public async Task QueElCierreProgramadoUseLaDuracionConfiguradaDesdeElCierreAnterior(int duracionDias)
    {
        // Arrange
        var apertura = new DateTimeOffset(2026, 4, 1, 3, 0, 0, TimeSpan.Zero);
        var cierre = apertura.AddDays(15);
        var cierreSiguiente = cierre.AddDays(duracionDias);
        var ronda = new RankingMusica { Apertura = apertura, Cierre = cierre };
        var reloj = new Mock<TimeProvider>(MockBehavior.Strict);
        reloj.Setup(reloj => reloj.GetUtcNow()).Returns(cierre.AddDays(3));
        var repositorio = new Mock<IRankingMusicaRepositorio>(MockBehavior.Strict);
        repositorio.Setup(repo => repo.ObtenerRondaAbierta(default)).ReturnsAsync(ronda);
        repositorio.Setup(repo => repo.CerrarRonda(ronda.Id, cierreSiguiente, default)).ReturnsAsync(true);
        var opciones = Options.Create(new RankingMusicaOptions { DuracionDias = duracionDias });
        var servicio = new RankingMusicaServicio(repositorio.Object, Mock.Of<ICategoriaServicio>(),
            Mock.Of<ICupoPuntosNostalgiaServicio>(), reloj.Object, opciones);

        // Act
        var proximoCierre = await servicio.ProcesarRonda();

        // Assert
        Assert.Equal(cierreSiguiente, proximoCierre);
        repositorio.Verify(repo => repo.CerrarRonda(ronda.Id, cierreSiguiente, default), Times.Once);
    }

    [Fact]
    public async Task QueLaPrimeraRondaUseElInicioYLaDuracionConfigurados()
    {
        // Arrange
        var opciones = Options.Create(new RankingMusicaOptions { FechaInicio = new DateOnly(2026, 4, 10), DuracionDias = 7 });
        var apertura = new DateTimeOffset(2026, 4, 10, 3, 0, 0, TimeSpan.Zero);
        var cierre = apertura.AddDays(7);
        var reloj = new Mock<TimeProvider>(MockBehavior.Strict);
        reloj.Setup(reloj => reloj.GetUtcNow()).Returns(apertura.AddDays(-1));
        var repositorio = new Mock<IRankingMusicaRepositorio>(MockBehavior.Strict);
        repositorio.Setup(repo => repo.ObtenerRondaAbierta(default)).ReturnsAsync((RankingMusica?)null);
        repositorio.Setup(repo => repo.CrearRondaInicial(apertura, cierre, default)).ReturnsAsync(true);
        var servicio = new RankingMusicaServicio(repositorio.Object, Mock.Of<ICategoriaServicio>(),
            Mock.Of<ICupoPuntosNostalgiaServicio>(), reloj.Object, opciones);

        // Act
        var proximoCierre = await servicio.ProcesarRonda();

        // Assert
        Assert.Equal(cierre, proximoCierre);
        repositorio.Verify(repo => repo.CrearRondaInicial(apertura, cierre, default), Times.Once);
    }
}
