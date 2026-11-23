using Moq;
using ProyectoFinalTPI.Backend.Dtos.Capsulas;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CapsulaPortadaTests
{
    private const string Nueva = "nostalgiar/capsulas/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Anterior = "nostalgiar/capsulas/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private readonly Mock<ICapsulaRepositorio> _repo = new(MockBehavior.Strict);
    private readonly Mock<IPortadaCapsulaAlmacenamiento> _archivos = new(MockBehavior.Strict);
    private readonly CapsulaServicio _servicio;
    // Firma mínima: el servicio valida formato/tamaño; el proveedor debe decodificar la imagen completa.
    private static MemoryStream Jpeg() => new(new byte[] { 255, 216, 255, 224, 0, 0, 0, 0, 0, 0, 0, 0 });
    private static ActualizarCapsulaDto Datos() => new() { Nombre = "Mis viajes" };
    private void Propietario() => _repo.Setup(r => r.Obtener(4, 10, default))
        .ReturnsAsync(new CapsulaDto { Id = 4, PropietarioId = 10 });
    private void Subida() => _archivos.Setup(a => a.Subir(It.IsAny<Stream>(), default)).ReturnsAsync(Nueva);

    public CapsulaPortadaTests()
    {
        var usuarios = new Mock<IUsuarioRepositorio>();
        usuarios.Setup(r => r.ObtenerPorIdAsync(10)).ReturnsAsync(new Usuario_Personal { Id = 10 });
        _servicio = new(_repo.Object, usuarios.Object, Mock.Of<IPublicacionServicio>(),
            Mock.Of<IEtiquetadoPublicacionServicio>(), TimeProvider.System, _archivos.Object);
    }

    [Fact]
    public async Task QueUnVisitanteNoPuedaSubirNiLeerUnaPortadaPrivada()
    {
        _repo.Setup(r => r.Obtener(4, 10, default)).ReturnsAsync(new CapsulaDto { Id = 4, PropietarioId = 20 });
        _repo.Setup(r => r.ObtenerRecursoPortada(4, null, default)).ReturnsAsync((string?)null);
        using var foto = Jpeg();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ActualizarConPortada(4, 10, Datos(), foto, "image/jpeg", default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerPortada(4, null, default));
        _archivos.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("image/png", 12)] // MIME y contenido no coinciden.
    [InlineData("image/svg+xml", 12)]
    [InlineData("image/jpeg", 0)]
    [InlineData("image/jpeg", 5242881)]
    public async Task QueSeRechacenArchivosInvalidosAntesDeContactarCloudinary(string mime, int longitud)
    {
        Propietario();
        var bytes = new byte[longitud];
        if (longitud >= 3) { bytes[0] = 255; bytes[1] = 216; bytes[2] = 255; }
        using var foto = new MemoryStream(bytes);
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ActualizarConPortada(4, 10, Datos(), foto, mime, default));
        _archivos.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueAlFallarElGuardadoSoloSeElimineLaFotoNueva()
    {
        Propietario(); Subida();
        _repo.Setup(r => r.ActualizarConPortada(4, 10, It.IsAny<ActualizarCapsulaDto>(), Nueva, default))
            .ThrowsAsync(new InvalidOperationException("Falló la base"));
        _archivos.Setup(a => a.Eliminar(Nueva, CancellationToken.None)).Returns(Task.CompletedTask);
        using var foto = Jpeg();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _servicio.ActualizarConPortada(4, 10, Datos(), foto, "image/jpeg", default));
        _archivos.Verify(a => a.Eliminar(Nueva, CancellationToken.None), Times.Once);
        _archivos.Verify(a => a.Eliminar(Anterior, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueReemplazarUnaPortadaLimpieLaAnteriorDespuesDeGuardar()
    {
        Propietario(); Subida();
        var guardada = false;
        _repo.Setup(r => r.ActualizarConPortada(4, 10, It.IsAny<ActualizarCapsulaDto>(), Nueva, default))
            .Callback(() => guardada = true).ReturnsAsync(Anterior);
        _archivos.Setup(a => a.Eliminar(Anterior, CancellationToken.None))
            .Callback(() => Assert.True(guardada)).Returns(Task.CompletedTask);
        using var foto = Jpeg();
        Assert.Equal(4, (await _servicio.ActualizarConPortada(4, 10, Datos(), foto, "image/jpeg", default)).Id);
        _archivos.Verify(a => a.Eliminar(Anterior, CancellationToken.None), Times.Once);
        _archivos.Verify(a => a.Eliminar(Nueva, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueUnaLecturaFallidaPosteriorAlCommitNoBorreLaPortadaNueva()
    {
        _repo.SetupSequence(r => r.Obtener(4, 10, default))
            .ReturnsAsync(new CapsulaDto { Id = 4, PropietarioId = 10 })
            .ThrowsAsync(new OperationCanceledException());
        Subida();
        _repo.Setup(r => r.ActualizarConPortada(4, 10, It.IsAny<ActualizarCapsulaDto>(), Nueva, default)).ReturnsAsync((string?)null);
        using var foto = Jpeg();
        await Assert.ThrowsAsync<OperationCanceledException>(() => _servicio.ActualizarConPortada(4, 10, Datos(), foto, "image/jpeg", default));
        _archivos.Verify(a => a.Eliminar(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueVolverAAutomaticaYLuegoEliminarLimpienSoloLasPortadasSubidas()
    {
        Propietario();
        _repo.Setup(r => r.Actualizar(4, 10, It.Is<ActualizarCapsulaDto>(d => !d.ConservarPortadaSubida), default)).ReturnsAsync(Anterior);
        _repo.Setup(r => r.Eliminar(4, 10, default)).ReturnsAsync((string?)null);
        _archivos.Setup(a => a.Eliminar(Anterior, CancellationToken.None)).Returns(Task.CompletedTask);
        await _servicio.Actualizar(4, 10, new() { Nombre = "Mis viajes", ConservarPortadaSubida = false }, default);
        await _servicio.Eliminar(4, 10, default);
        _archivos.Verify(a => a.Eliminar(Anterior, CancellationToken.None), Times.Once);
    }
}
