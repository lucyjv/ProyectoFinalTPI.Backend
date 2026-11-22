using Microsoft.EntityFrameworkCore;
using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class RankingMusicaRepositorioUnitTests
{
    private static readonly DateTimeOffset Apertura = new(2026, 4, 1, 3, 0, 0, TimeSpan.Zero);
    private static readonly Decada AñosOchenta = new(1980);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task QueUnaRondaConCierreInvalidoSeRechaceAntesDeAccederALaBase(int diasHastaCierre)
    {
        // Arrange
        var contexto = new Mock<ApplicationDbContext>(MockBehavior.Strict, OpcionesEnMemoria());
        var reloj = new Mock<TimeProvider>(MockBehavior.Strict);
        var repositorio = CrearRepositorio(contexto.Object, reloj.Object);
        var cierre = Apertura.AddDays(diasHastaCierre);

        // Act
        var error = await Assert.ThrowsAsync<ArgumentException>(() => repositorio.CrearRondaInicial(Apertura, cierre));

        // Assert
        Assert.Equal("cierre", error.ParamName);
        contexto.VerifyNoOtherCalls();
        reloj.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task QueUnVotoSinPuntosPositivosSeRechaceAntesDeAbrirUnaTransaccion(int puntos)
    {
        // Arrange
        var contexto = new Mock<ApplicationDbContext>(MockBehavior.Strict, OpcionesEnMemoria());
        var reloj = new Mock<TimeProvider>(MockBehavior.Strict);
        var repositorio = CrearRepositorio(contexto.Object, reloj.Object);

        // Act
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repositorio.RegistrarVoto(candidaturaId: 7, usuarioId: 21, puntos: puntos, cupoDiario: 5));

        // Assert
        Assert.Equal("puntos", error.ParamName);
        contexto.VerifyNoOtherCalls();
        reloj.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueLasCandidaturasMuestrenElTotalYExcluyanRetiradasYOtrasRondas()
    {
        // Arrange
        var opciones = OpcionesEnMemoria();
        await using var datos = new ApplicationDbContext(opciones);
        var activa = new CandidaturaRankingMusica(7, 21, AñosOchenta, "Canción activa", "Banda", Apertura);
        var retirada = new CandidaturaRankingMusica(7, 21, AñosOchenta, "Canción retirada", "Banda", Apertura)
        {
            FechaRetiro = Apertura.AddDays(1)
        };
        var otraRonda = new CandidaturaRankingMusica(8, 21, AñosOchenta, "Otra ronda", "Banda", Apertura);
        datos.AddRange(activa, retirada, otraRonda);
        await datos.SaveChangesAsync();
        datos.AddRange(
            new VotoRankingMusica { CandidaturaId = activa.Id, UsuarioId = 21, Puntos = 3, FechaVoto = Apertura },
            new VotoRankingMusica { CandidaturaId = activa.Id, UsuarioId = 22, Puntos = 5, FechaVoto = Apertura },
            new VotoRankingMusica { CandidaturaId = retirada.Id, UsuarioId = 21, Puntos = 7, FechaVoto = Apertura });
        await datos.SaveChangesAsync();
        var contexto = new Mock<ApplicationDbContext>(MockBehavior.Strict, opciones);
        contexto.Setup(db => db.Set<CandidaturaRankingMusica>()).Returns(datos.Set<CandidaturaRankingMusica>());
        contexto.Setup(db => db.Set<VotoRankingMusica>()).Returns(datos.Set<VotoRankingMusica>());
        var reloj = new Mock<TimeProvider>(MockBehavior.Strict);
        var repositorio = CrearRepositorio(contexto.Object, reloj.Object);

        // Act
        var candidaturas = await repositorio.ListarCandidaturas(7);

        // Assert
        var fila = Assert.Single(candidaturas);
        Assert.Equal(activa.Id, fila.Candidatura.Id);
        Assert.Equal(8L, fila.Puntos);
        contexto.Verify(db => db.Set<CandidaturaRankingMusica>(), Times.Once);
        contexto.Verify(db => db.Set<VotoRankingMusica>(), Times.Once);
        contexto.VerifyNoOtherCalls();
        reloj.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueUnaRondaAbiertaNoDevuelvaResultadosNiConsulteSusPosiciones()
    {
        // Arrange
        var opciones = OpcionesEnMemoria();
        await using var datos = new ApplicationDbContext(opciones);
        var ronda = new RankingMusica { Apertura = Apertura, Cierre = Apertura.AddDays(15) };
        datos.Add(ronda);
        await datos.SaveChangesAsync();
        var contexto = new Mock<ApplicationDbContext>(MockBehavior.Strict, opciones);
        contexto.Setup(db => db.Set<RankingMusica>()).Returns(datos.Set<RankingMusica>());
        var reloj = new Mock<TimeProvider>(MockBehavior.Strict);
        var repositorio = CrearRepositorio(contexto.Object, reloj.Object);

        // Act
        var resultado = await repositorio.ObtenerResultados(ronda.Id);

        // Assert
        Assert.Null(resultado);
        contexto.Verify(db => db.Set<RankingMusica>(), Times.Once);
        contexto.Verify(db => db.Set<ResultadoRankingMusica>(), Times.Never);
        contexto.VerifyNoOtherCalls();
        reloj.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueConsultarResultadosTomeLaUltimaRondaCerradaYDevuelvaLaCancionEnSuPuesto()
    {
        // Arrange
        var opciones = OpcionesEnMemoria();
        await using var datos = new ApplicationDbContext(opciones);
        var anterior = new RankingMusica { Apertura = Apertura, Cierre = Apertura.AddDays(15), Estado = EstadoRankingMusica.Cerrado };
        var ultima = new RankingMusica { Apertura = anterior.Cierre, Cierre = anterior.Cierre.AddDays(15), Estado = EstadoRankingMusica.Cerrado };
        var abierta = new RankingMusica { Apertura = ultima.Cierre, Cierre = ultima.Cierre.AddDays(15) };
        datos.AddRange(anterior, ultima, abierta);
        await datos.SaveChangesAsync();
        var ganadora = new CandidaturaRankingMusica(ultima.Id, 21, AñosOchenta, "Canción ganadora", "Banda", ultima.Apertura);
        datos.Add(ganadora);
        await datos.SaveChangesAsync();
        datos.Add(new ResultadoRankingMusica
        {
            RankingMusicaId = ultima.Id, CandidaturaId = ganadora.Id, Decada = AñosOchenta, Posicion = 1, Puntos = 9
        });
        await datos.SaveChangesAsync();
        var contexto = new Mock<ApplicationDbContext>(MockBehavior.Strict, opciones);
        contexto.Setup(db => db.Set<RankingMusica>()).Returns(datos.Set<RankingMusica>());
        contexto.Setup(db => db.Set<ResultadoRankingMusica>()).Returns(datos.Set<ResultadoRankingMusica>());
        contexto.Setup(db => db.Set<CandidaturaRankingMusica>()).Returns(datos.Set<CandidaturaRankingMusica>());
        var reloj = new Mock<TimeProvider>(MockBehavior.Strict);
        var repositorio = CrearRepositorio(contexto.Object, reloj.Object);

        // Act
        var resultado = await repositorio.ObtenerResultados();

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(ultima.Id, resultado.Ronda.Id);
        var posicion = Assert.Single(resultado.Posiciones);
        Assert.Equal(ganadora.Titulo, posicion.Titulo);
        Assert.Equal(ganadora.Artista, posicion.Artista);
        Assert.Equal(1, posicion.Resultado.Posicion);
        Assert.Equal(9L, posicion.Resultado.Puntos);
    }

    [Theory]
    [InlineData(21, 1, 4)]
    [InlineData(21, 2, 2)]
    [InlineData(21, 3, 0)]
    [InlineData(22, 1, 7)]
    [InlineData(23, 1, 0)]
    public async Task QueElConsumoSeConsultePorUsuarioYFecha(int usuarioId, int dia, int esperado)
    {
        // Arrange
        var opciones = OpcionesEnMemoria();
        await using var datos = new ApplicationDbContext(opciones);
        datos.AddRange(
            new ConsumoDiarioPuntosNostalgia { UsuarioId = 21, Fecha = new DateOnly(2026, 4, 1), PuntosGastados = 4 },
            new ConsumoDiarioPuntosNostalgia { UsuarioId = 21, Fecha = new DateOnly(2026, 4, 2), PuntosGastados = 2 },
            new ConsumoDiarioPuntosNostalgia { UsuarioId = 22, Fecha = new DateOnly(2026, 4, 1), PuntosGastados = 7 });
        await datos.SaveChangesAsync();
        var contexto = new Mock<ApplicationDbContext>(MockBehavior.Strict, opciones);
        contexto.Setup(db => db.Set<ConsumoDiarioPuntosNostalgia>()).Returns(datos.Set<ConsumoDiarioPuntosNostalgia>());
        var consumo = new ConsumoDiarioPuntosNostalgiaRepositorio(contexto.Object);
        var fecha = new DateOnly(2026, 4, dia);

        // Act
        var puntos = await consumo.ObtenerPuntosGastados(usuarioId, fecha);

        // Assert
        Assert.Equal(esperado, puntos);
        contexto.Verify(db => db.Set<ConsumoDiarioPuntosNostalgia>(), Times.Once);
        contexto.VerifyNoOtherCalls();
    }

    private static RankingMusicaRepositorio CrearRepositorio(ApplicationDbContext contexto, TimeProvider reloj) =>
        new(contexto, new ConsumoDiarioPuntosNostalgiaRepositorio(contexto), reloj);

    private static DbContextOptions<ApplicationDbContext> OpcionesEnMemoria() =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
}
