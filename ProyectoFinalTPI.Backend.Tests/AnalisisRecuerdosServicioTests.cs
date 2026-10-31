using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AnalisisRecuerdosServicioTests
{
    private readonly Mock<ITrabajoAnalisisRepositorio> _repositorioMock = new();
    private readonly Mock<IAnalizadorRecuerdo> _analizadorMock = new();
    private readonly Mock<IRelacionRecuerdosRepositorio> _relacionesMock = new();
    private readonly OpcionesAnalisisRecuerdos _opciones = new();
    private readonly AnalisisRecuerdosServicio _servicio;

    private ReservaAnalisisDto? _reserva = new()
    {
        Id = 1,
        PublicacionId = 10,
        VersionContenido = 1,
        Intentos = 1,
        TokenReserva = Guid.NewGuid(),
        ReservadoHasta = DateTime.UtcNow.AddMinutes(2)
    };
    private EntradaAnalisisRecuerdoDto? _entrada = new(10, 1, "México 86", "Los festejos", DateTime.UtcNow);

    public AnalisisRecuerdosServicioTests()
    {
        _analizadorMock.SetupGet(a => a.Version).Returns("test-v1");
        _relacionesMock.Setup(r => r.ObtenerOriginalVisible(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(10);

        _repositorioMock
            .Setup(r => r.Reservar(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _reserva);

        _repositorioMock
            .Setup(r => r.ObtenerEntrada(It.IsAny<ReservaAnalisisDto>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _entrada);

        _repositorioMock
            .Setup(r => r.Completar(It.IsAny<ReservaAnalisisDto>(), It.IsAny<ResultadoAnalisisRecuerdoDto>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _servicio = new(
            _repositorioMock.Object,
            _analizadorMock.Object,
            _relacionesMock.Object,
            Options.Create(_opciones),
            TimeProvider.System,
            NullLogger<AnalisisRecuerdosServicio>.Instance);
    }

    #region Procesamiento de Trabajos en Cola

    [Fact]
    public async Task QueNoSeInvoqueAlAnalizadorCuandoLaColaEstaVacia()
    {
        //Arrange
        _reserva = null;

        //Act
        var result = await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.False(result);
        _analizadorMock.Verify(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositorioMock.Verify(r => r.Completar(It.IsAny<ReservaAnalisisDto>(), It.IsAny<ResultadoAnalisisRecuerdoDto>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueSeAnaliceElContenidoReservadoYSeGuardeLaVersionDelAnalizador()
    {
        //Arrange
        var resultadoEsperado = new ResultadoAnalisisRecuerdoDto(new[] { new MomentoDetectadoDto(1, 0.9) });
        _analizadorMock
            .Setup(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultadoEsperado);

        //Act
        var result = await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.True(result);
        _analizadorMock.Verify(a => a.Analizar(_entrada!, It.IsAny<CancellationToken>()), Times.Once);
        _repositorioMock.Verify(r => r.Completar(_reserva!, resultadoEsperado, "test-v1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueSeDescarteUnTrabajoDesactualizadoSinLlamarAlAnalizador()
    {
        //Arrange
        _entrada = null;

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        _repositorioMock.Verify(r => r.Descartar(_reserva!, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _analizadorMock.Verify(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Validaciones y Errores de Análisis

    [Fact]
    public async Task QueUnFalloDelProveedorProgrameUnReintentoSinGuardarSuMensajePrivado()
    {
        //Arrange
        _analizadorMock
            .Setup(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Texto privado del proveedor"));

        //Act
        var result = await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.True(result);
        _repositorioMock.Verify(r => r.RegistrarFallo(
            _reserva!,
            "analisis_error",
            It.IsAny<DateTime>(),
            It.Is<DateTime>(sig => sig > DateTime.UtcNow.AddSeconds(8) && sig < DateTime.UtcNow.AddSeconds(12)),
            3,
            It.IsAny<CancellationToken>()), Times.Once);
        _repositorioMock.Verify(r => r.Completar(It.IsAny<ReservaAnalisisDto>(), It.IsAny<ResultadoAnalisisRecuerdoDto>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task QueNoSeGuardenResultadosInvalidosNiSeReintentenIndefinidamente(int caso)
    {
        //Arrange
        ResultadoAnalisisRecuerdoDto resultadoInvalido = caso switch
        {
            0 => new ResultadoAnalisisRecuerdoDto(new[] { new MomentoDetectadoDto(1, double.NaN) }),
            1 => new ResultadoAnalisisRecuerdoDto(new[] { new MomentoDetectadoDto(1, 0.9), new MomentoDetectadoDto(1, 0.8) }),
            2 => new ResultadoAnalisisRecuerdoDto(new[] { new MomentoDetectadoDto(0, 0.9) }),
            _ => new ResultadoAnalisisRecuerdoDto(new[] { new MomentoDetectadoDto(1, 0.9) }, AnalisisRealizado: false)
        };
        _analizadorMock
            .Setup(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultadoInvalido);

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        _repositorioMock.Verify(r => r.Completar(It.IsAny<ReservaAnalisisDto>(), It.IsAny<ResultadoAnalisisRecuerdoDto>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositorioMock.Verify(r => r.RegistrarFallo(
            _reserva!,
            "analisis_invalido",
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueUnaEntradaDemasiadoGrandeNoSeEnvieAlProveedor()
    {
        //Arrange
        _opciones.MaxCaracteresEntrada = 100;
        _entrada = new(10, 1, "Título", new string('a', 101), DateTime.UtcNow);

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        _analizadorMock.Verify(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositorioMock.Verify(r => r.RegistrarFallo(
            _reserva!,
            "analisis_invalido",
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueUnAnalizadorQueNoRespondeTengaTiempoMaximo()
    {
        //Arrange
        _opciones.TiempoMaximoSegundos = 1;
        var pendiente = new TaskCompletionSource<ResultadoAnalisisRecuerdoDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        _analizadorMock
            .Setup(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()))
            .Returns(pendiente.Task);

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);
        pendiente.SetResult(new(Array.Empty<MomentoDetectadoDto>()));

        //Assert
        _repositorioMock.Verify(r => r.RegistrarFallo(
            _reserva!,
            "tiempo_agotado",
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _repositorioMock.Verify(r => r.Completar(It.IsAny<ReservaAnalisisDto>(), It.IsAny<ResultadoAnalisisRecuerdoDto>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueLaCancelacionDelHostNoSeRegistreComoUnFalloDelProveedor()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();
        _analizadorMock
            .Setup(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()))
            .Returns<EntradaAnalisisRecuerdoDto, CancellationToken>((_, ct) =>
            {
                cancelacion.Cancel();
                return Task.FromCanceled<ResultadoAnalisisRecuerdoDto>(ct);
            });

        //Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _servicio.ProcesarUno(cancelacion.Token));

        //Assert
        _repositorioMock.Verify(r => r.RegistrarFallo(It.IsAny<ReservaAnalisisDto>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositorioMock.Verify(r => r.Completar(It.IsAny<ReservaAnalisisDto>(), It.IsAny<ResultadoAnalisisRecuerdoDto>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueElReintentoPorCuotaRespeteLaEsperaDelProveedor()
    {
        //Arrange
        _analizadorMock
            .Setup(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ErrorProveedorAnalisis("proveedor_cuota", reintentarEn: TimeSpan.FromMinutes(2)));

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        _repositorioMock.Verify(r => r.RegistrarFallo(
            _reserva!,
            "proveedor_cuota",
            It.IsAny<DateTime>(),
            It.Is<DateTime>(sig => sig > DateTime.UtcNow.AddMinutes(1.9) && sig < DateTime.UtcNow.AddMinutes(2.1)),
            3,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueUnaClaveInvalidaNoSeReintenteComoSiFueraUnErrorTransitorio()
    {
        //Arrange
        _analizadorMock
            .Setup(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ErrorProveedorAnalisis("proveedor_credenciales", permanente: true));

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        _repositorioMock.Verify(r => r.RegistrarFallo(
            _reserva!,
            "proveedor_credenciales",
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            1,
            It.IsAny<CancellationToken>()), Times.Once);
        _repositorioMock.Verify(r => r.Completar(It.IsAny<ReservaAnalisisDto>(), It.IsAny<ResultadoAnalisisRecuerdoDto>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueUnaIdentidadAmbiguaRequieraRevisionSinRepetirLlamadasAlProveedor()
    {
        //Arrange
        _analizadorMock
            .Setup(a => a.Analizar(It.IsAny<EntradaAnalisisRecuerdoDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProyectoFinalTPI.Backend.Entidades.ConflictoCatalogoException("Existen identidades duplicadas."));

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        _repositorioMock.Verify(r => r.RegistrarFallo(
            _reserva!,
            "catalogo_ambiguo",
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            1,
            It.IsAny<CancellationToken>()), Times.Once);
        _repositorioMock.Verify(r => r.Completar(It.IsAny<ReservaAnalisisDto>(), It.IsAny<ResultadoAnalisisRecuerdoDto>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Consulta de Estado de Análisis

    [Fact]
    public async Task QueElEstadoDeUnRepostSeConsulteSobreSuOriginalVisible()
    {
        //Arrange
        var idRepost = 25;
        _repositorioMock
            .Setup(r => r.ObtenerEstado(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstadoAnalisisRecuerdoDto { PublicacionId = 10, Estado = "pendiente" });

        //Act
        var result = await _servicio.ObtenerEstado(idRepost, CancellationToken.None);

        //Assert
        Assert.Equal("pendiente", result.Estado);
        _repositorioMock.Verify(r => r.ObtenerEstado(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNoSeExpongaElEstadoDeUnRecuerdoOcultoOInexistente()
    {
        //Arrange
        _relacionesMock
            .Setup(r => r.ObtenerOriginalVisible(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        //Act
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerEstado(99, CancellationToken.None));

        //Assert
        _repositorioMock.Verify(r => r.ObtenerEstado(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
