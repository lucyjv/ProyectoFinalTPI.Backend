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
    private readonly RepositorioFake _repositorio = new();
    private readonly AnalizadorFake _analizador = new();
    private readonly Mock<IRelacionRecuerdosRepositorio> _relaciones = new(MockBehavior.Strict);
    private readonly OpcionesAnalisisRecuerdos _opciones = new();
    private readonly AnalisisRecuerdosServicio _servicio;

    public AnalisisRecuerdosServicioTests()
    {
        _relaciones.Setup(r => r.ObtenerOriginalVisible(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(10);
        _servicio = new(_repositorio, _analizador, _relaciones.Object, Options.Create(_opciones), TimeProvider.System, NullLogger<AnalisisRecuerdosServicio>.Instance);
    }

    [Fact]
    public async Task QueNoSeInvoqueAlAnalizadorCuandoLaColaEstaVacia()
    {
        //Arrange
        _repositorio.Reserva = null;

        //Act
        var result = await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.False(result);
        Assert.Equal(0, _analizador.Llamadas);
        Assert.Null(_repositorio.ResultadoGuardado);
    }

    [Fact]
    public async Task QueSeAnaliceElContenidoReservadoYSeGuardeLaVersionDelAnalizador()
    {
        //Arrange
        _analizador.Resultado = new(new[] { new MomentoDetectadoDto(1, 0.9) });

        //Act
        var result = await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.True(result);
        Assert.Equal(_repositorio.Entrada, _analizador.EntradaRecibida);
        Assert.Same(_analizador.Resultado, _repositorio.ResultadoGuardado);
        Assert.Equal("test-v1", _repositorio.VersionGuardada);
        Assert.Equal(0, _repositorio.Fallos);
    }

    [Fact]
    public async Task QueSeDescarteUnTrabajoDesactualizadoSinLlamarAlAnalizador()
    {
        //Arrange
        _repositorio.Entrada = null;

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.True(_repositorio.Descartado);
        Assert.Equal(0, _analizador.Llamadas);
    }

    [Fact]
    public async Task QueUnFalloDelProveedorProgrameUnReintentoSinGuardarSuMensajePrivado()
    {
        //Arrange
        _analizador.Error = new InvalidOperationException("Texto privado del proveedor");

        //Act
        var result = await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.True(result);
        Assert.Equal("analisis_error", _repositorio.Codigo);
        Assert.Equal(3, _repositorio.MaxIntentosFallo);
        Assert.InRange((_repositorio.SiguienteIntento - _repositorio.AhoraFallo).TotalSeconds, 9, 11);
        Assert.Null(_repositorio.ResultadoGuardado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task QueNoSeGuardenResultadosInvalidosNiSeReintentenIndefinidamente(int caso)
    {
        //Arrange
        _analizador.Resultado = caso switch
        {
            0 => new(new[] { new MomentoDetectadoDto(1, double.NaN) }),
            1 => new(new[] { new MomentoDetectadoDto(1, 0.9), new MomentoDetectadoDto(1, 0.8) }),
            2 => new(new[] { new MomentoDetectadoDto(0, 0.9) }),
            _ => new(new[] { new MomentoDetectadoDto(1, 0.9) }, AnalisisRealizado: false)
        };

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.Null(_repositorio.ResultadoGuardado);
        Assert.Equal("analisis_invalido", _repositorio.Codigo);
        Assert.Equal(1, _repositorio.MaxIntentosFallo);
    }

    [Fact]
    public async Task QueUnaEntradaDemasiadoGrandeNoSeEnvieAlProveedor()
    {
        //Arrange
        _opciones.MaxCaracteresEntrada = 100;
        _repositorio.Entrada = new(10, 1, "Título", new string('a', 101), DateTime.UtcNow);

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.Equal(0, _analizador.Llamadas);
        Assert.Equal("analisis_invalido", _repositorio.Codigo);
    }

    [Fact]
    public async Task QueUnAnalizadorQueNoRespondeTengaTiempoMaximo()
    {
        //Arrange
        _opciones.TiempoMaximoSegundos = 1;
        var pendiente = new TaskCompletionSource<ResultadoAnalisisRecuerdoDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        _analizador.Operacion = (_, _) => pendiente.Task;

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);
        pendiente.SetResult(new(Array.Empty<MomentoDetectadoDto>()));

        //Assert
        Assert.Equal("tiempo_agotado", _repositorio.Codigo);
        Assert.Null(_repositorio.ResultadoGuardado);
    }

    [Fact]
    public async Task QueLaCancelacionDelHostNoSeRegistreComoUnFalloDelProveedor()
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();
        _analizador.Operacion = (_, ct) => { cancelacion.Cancel(); return Task.FromCanceled<ResultadoAnalisisRecuerdoDto>(ct); };

        //Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _servicio.ProcesarUno(cancelacion.Token));

        //Assert
        Assert.Equal(0, _repositorio.Fallos);
        Assert.Null(_repositorio.ResultadoGuardado);
    }

    [Fact]
    public async Task QueElEstadoDeUnRepostSeConsulteSobreSuOriginalVisible()
    {
        //Arrange
        var idRepost = 25;

        //Act
        var result = await _servicio.ObtenerEstado(idRepost, CancellationToken.None);

        //Assert
        Assert.Equal(10, _repositorio.PublicacionConsultada);
        Assert.Equal("pendiente", result.Estado);
    }

    [Fact]
    public async Task QueNoSeExpongaElEstadoDeUnRecuerdoOcultoOInexistente()
    {
        //Arrange
        _relaciones.Setup(r => r.ObtenerOriginalVisible(99, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        //Act
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerEstado(99, CancellationToken.None));

        //Assert
        Assert.Null(_repositorio.PublicacionConsultada);
    }

    [Fact]
    public async Task QueElReintentoPorCuotaRespeteLaEsperaDelProveedor()
    {
        //Arrange
        _analizador.Error = new ErrorProveedorAnalisis("proveedor_cuota", reintentarEn: TimeSpan.FromMinutes(2));

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.Equal("proveedor_cuota", _repositorio.Codigo);
        Assert.Equal(TimeSpan.FromMinutes(2), _repositorio.SiguienteIntento - _repositorio.AhoraFallo);
        Assert.Equal(3, _repositorio.MaxIntentosFallo);
    }

    [Fact]
    public async Task QueUnaClaveInvalidaNoSeReintenteComoSiFueraUnErrorTransitorio()
    {
        //Arrange
        _analizador.Error = new ErrorProveedorAnalisis("proveedor_credenciales", permanente: true);

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.Equal("proveedor_credenciales", _repositorio.Codigo);
        Assert.Equal(1, _repositorio.MaxIntentosFallo);
        Assert.Null(_repositorio.ResultadoGuardado);
    }

    [Fact]
    public async Task QueUnaIdentidadAmbiguaRequieraRevisionSinRepetirLlamadasAlProveedor()
    {
        //Arrange
        _analizador.Error = new ProyectoFinalTPI.Backend.Entidades.ConflictoCatalogoException("Existen identidades duplicadas.");

        //Act
        await _servicio.ProcesarUno(CancellationToken.None);

        //Assert
        Assert.Equal("catalogo_ambiguo", _repositorio.Codigo);
        Assert.Equal(1, _repositorio.MaxIntentosFallo);
        Assert.Null(_repositorio.ResultadoGuardado);
    }

    private sealed class AnalizadorFake : IAnalizadorRecuerdo
    {
        public string Version => "test-v1";
        public int Llamadas;
        public Exception? Error;
        public EntradaAnalisisRecuerdoDto? EntradaRecibida;
        public ResultadoAnalisisRecuerdoDto Resultado = new(Array.Empty<MomentoDetectadoDto>());
        public Func<EntradaAnalisisRecuerdoDto, CancellationToken, Task<ResultadoAnalisisRecuerdoDto>>? Operacion;
        public Task<ResultadoAnalisisRecuerdoDto> Analizar(EntradaAnalisisRecuerdoDto entrada, CancellationToken ct)
        {
            Llamadas++; EntradaRecibida = entrada;
            if (Error != null) throw Error;
            return Operacion?.Invoke(entrada, ct) ?? Task.FromResult(Resultado);
        }
    }

    private sealed class RepositorioFake : ITrabajoAnalisisRepositorio
    {
        public ReservaAnalisisDto? Reserva = new() { Id = 1, PublicacionId = 10, VersionContenido = 1, Intentos = 1, TokenReserva = Guid.NewGuid(), ReservadoHasta = DateTime.UtcNow.AddMinutes(2) };
        public EntradaAnalisisRecuerdoDto? Entrada = new(10, 1, "México 86", "Los festejos", DateTime.UtcNow);
        public ResultadoAnalisisRecuerdoDto? ResultadoGuardado;
        public string? VersionGuardada, Codigo;
        public bool Descartado;
        public int Fallos, MaxIntentosFallo;
        public int? PublicacionConsultada;
        public DateTime SiguienteIntento, AhoraFallo;
        public Task<ReservaAnalisisDto?> Reservar(DateTime ahora, TimeSpan duracion, int maxIntentos, CancellationToken ct) => Task.FromResult(Reserva);
        public Task<EntradaAnalisisRecuerdoDto?> ObtenerEntrada(ReservaAnalisisDto reserva, DateTime ahora, CancellationToken ct) => Task.FromResult(Entrada);
        public Task<bool> Completar(ReservaAnalisisDto reserva, ResultadoAnalisisRecuerdoDto resultado, string versionAnalizador, DateTime ahora, CancellationToken ct)
        { ResultadoGuardado = resultado; VersionGuardada = versionAnalizador; return Task.FromResult(true); }
        public Task Descartar(ReservaAnalisisDto reserva, DateTime ahora, CancellationToken ct) { Descartado = true; return Task.CompletedTask; }
        public Task RegistrarFallo(ReservaAnalisisDto reserva, string codigo, DateTime ahora, DateTime siguienteIntento, int maxIntentos, CancellationToken ct)
        { Fallos++; Codigo = codigo; AhoraFallo = ahora; SiguienteIntento = siguienteIntento; MaxIntentosFallo = maxIntentos; return Task.CompletedTask; }
        public Task<EstadoAnalisisRecuerdoDto?> ObtenerEstado(int publicacionId, CancellationToken ct)
        { PublicacionConsultada = publicacionId; return Task.FromResult<EstadoAnalisisRecuerdoDto?>(new() { PublicacionId = publicacionId, Estado = "pendiente" }); }
    }
}
