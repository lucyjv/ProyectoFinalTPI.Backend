using Moq;
using ProyectoFinalTPI.Backend.Dtos.Capsulas;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CapsulaServicioTests
{
    private readonly Mock<ICapsulaRepositorio> _capsulas = new(MockBehavior.Strict);
    private readonly Mock<IUsuarioRepositorio> _usuarios = new();
    private readonly Mock<IPublicacionServicio> _publicaciones = new(MockBehavior.Strict);
    private readonly Mock<IEtiquetadoPublicacionServicio> _etiquetas = new(MockBehavior.Strict);
    private readonly CapsulaServicio _servicio;
    private static readonly DateTime Fecha = new(2022, 12, 18, 0, 0, 0, DateTimeKind.Utc);

    public CapsulaServicioTests()
    {
        _usuarios.Setup(r => r.ObtenerPorIdAsync(10)).ReturnsAsync(new Usuario_Personal { Id = 10 });
        _servicio = new(_capsulas.Object, _usuarios.Object, _publicaciones.Object, _etiquetas.Object,
            new RelojCategoria(new DateTimeOffset(Fecha)));
    }

    #region Creación y permisos

    [Fact]
    public async Task QueCrearNormaliceLosDatosUsePrivadaPorDefectoYQuiteIdsRepetidos()
    {
        // Arrange
        _capsulas.Setup(r => r.Crear(It.Is<Capsula>(c => c.PropietarioId == 10 && c.Nombre == "Mi infancia" &&
            c.Descripcion == "Recuerdos" && c.Visibilidad == VisibilidadCapsula.Privada &&
            c.FechaCreacion == Fecha && c.FechaActualizacion == Fecha),
            It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 7, 8 })), default)).ReturnsAsync(4);
        _capsulas.Setup(r => r.Obtener(4, 10, default)).ReturnsAsync(new CapsulaDto { Id = 4, PropietarioId = 10 });

        // Act
        var resultado = await _servicio.Crear(10, new CrearCapsulaDto
            { Nombre = "  Mi infancia  ", Descripcion = " Recuerdos ", PublicacionesIds = new() { 7, 7, 8 } }, default);

        // Assert
        Assert.Equal(4, resultado.Id);
        _capsulas.VerifyAll();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public async Task QueUnNombreInvalidoSeRechaceAntesDeGuardar(string nombre)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Crear(10, new() { Nombre = nombre }, default));
        _capsulas.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task QueNoSeAceptenIdentificadoresInvalidos(int id)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Crear(10,
            new() { Nombre = "Mis viajes", PublicacionesIds = new() { id } }, default));
        _capsulas.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueNoSeAceptenMasDeCienIdsNiUnaVisibilidadInexistente()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Crear(10,
            new() { Nombre = "Mis viajes", PublicacionesIds = Enumerable.Range(1, 101).ToList() }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Crear(10,
            new() { Nombre = "Mis viajes", Visibilidad = (VisibilidadCapsula)99 }, default));
        _capsulas.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueUnUsuarioEliminadoNoPuedaCrearCapsulas()
    {
        _usuarios.Setup(r => r.ObtenerPorIdAsync(10)).ReturnsAsync(new Usuario_Personal { Id = 10, EstaEliminado = true });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Crear(10, new() { Nombre = "Viajes" }, default));
        _capsulas.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueSerVisitanteDeUnaCapsulaPublicaNoPermitaAdministrarla()
    {
        _capsulas.Setup(r => r.Obtener(4, 10, default)).ReturnsAsync(new CapsulaDto { Id = 4, PropietarioId = 20 });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Eliminar(4, 10, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Actualizar(4, 10, new() { Nombre = "Cambio" }, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.AgregarRecuerdos(4, 10, new[] { 7 }, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.QuitarRecuerdo(4, 10, 7, default));
        _capsulas.Verify(r => r.Eliminar(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task QueUnaCapsulaInaccesibleNoConsulteRecuerdosNiMapa()
    {
        _capsulas.Setup(r => r.Obtener(4, null, default)).ReturnsAsync((CapsulaDto?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerRecuerdos(4, null, null, 20, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.ObtenerMapa(4, null, new(), null, 20, default));
        _publicaciones.VerifyNoOtherCalls();
        _capsulas.Verify(r => r.ObtenerReferencias(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DateTime?>(),
            It.IsAny<int?>(), It.IsAny<int>(), default), Times.Never);
        _capsulas.Verify(r => r.ObtenerPines(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<ConsultaMapaCapsulaDto>(),
            It.IsAny<int?>(), It.IsAny<int>(), default), Times.Never);
    }

    #endregion

    #region Paginación y mapa

    [Fact]
    public async Task QueElHiloPidaLosDetallesYLasEtiquetasEnBloqueYContinueSinRepetirElUltimo()
    {
        // Arrange: la tercera referencia solo sirve para saber que hay otra página.
        _capsulas.Setup(r => r.Obtener(4, 10, default)).ReturnsAsync(new CapsulaDto { Id = 4, PropietarioId = 10 });
        _capsulas.Setup(r => r.ObtenerReferencias(4, 10, null, null, 3, default)).ReturnsAsync(new List<ReferenciaRecuerdoCapsulaDto>()
            { new(7, Fecha), new(8, Fecha), new(9, Fecha.AddDays(1)) });
        var detalles = new List<PublicacionDetalleDto> { new() { Id = 7 }, new() { Id = 8 } };
        _publicaciones.Setup(p => p.ObtenerDetallesPorIds(It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 7, 8 })),
            10, default)).ReturnsAsync(detalles);
        _etiquetas.Setup(e => e.CompletarEtiquetas(detalles, default)).Returns(Task.CompletedTask);
        _capsulas.Setup(r => r.ObtenerReferencias(4, 10, Fecha, 8, 3, default)).ReturnsAsync(new List<ReferenciaRecuerdoCapsulaDto>());
        _publicaciones.Setup(p => p.ObtenerDetallesPorIds(It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 0), 10, default))
            .ReturnsAsync(new List<PublicacionDetalleDto>());
        _etiquetas.Setup(e => e.CompletarEtiquetas(It.Is<IReadOnlyCollection<PublicacionDetalleDto>>(p => p.Count == 0), default))
            .Returns(Task.CompletedTask);

        // Act
        var primera = await _servicio.ObtenerRecuerdos(4, 10, null, 2, default);
        var segunda = await _servicio.ObtenerRecuerdos(4, 10, primera.SiguienteCursor, 2, default);

        // Assert
        Assert.Equal(new[] { 7, 8 }, primera.Items.Select(p => p.Id));
        Assert.True(primera.HayMas);
        Assert.Empty(segunda.Items);
        Assert.False(segunda.HayMas);
        _publicaciones.VerifyAll();
        _capsulas.VerifyAll();
    }

    [Fact]
    public async Task QueUnCursorNoPuedaReutilizarseEnOtroListadoNiEnOtroUsuario()
    {
        _usuarios.Setup(r => r.ObtenerPorIdAsync(20)).ReturnsAsync(new Usuario_Personal { Id = 20 });
        _capsulas.Setup(r => r.Listar(10, true, null, 2, default)).ReturnsAsync(new List<CapsulaDto>() { new() { Id = 4 }, new() { Id = 3 } });
        var pagina = await _servicio.Listar(10, true, null, 1, default);
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Listar(20, true, pagina.SiguienteCursor, 1, default));
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Listar(10, false, pagina.SiguienteCursor, 1, default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-cursor")]
    [InlineData("e30")]
    public async Task QueLosCursoresMalformadosSeRechacen(string cursor)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Listar(10, true, cursor, 20, default));
        _capsulas.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueElMapaDevuelvaLimitesGlobalesAunqueSoloSePidaUnPin()
    {
        _capsulas.Setup(r => r.Obtener(4, null, default)).ReturnsAsync(new CapsulaDto { Id = 4 });
        _capsulas.Setup(r => r.ObtenerPines(4, null, It.IsAny<ConsultaMapaCapsulaDto>(), null, 2, default))
            .ReturnsAsync(new List<PublicacionPinDto>() { new() { Id = 7 }, new() { Id = 8 } });
        var limites = new LimitesMapaCapsulaDto(-35, 50, -60, 150);
        _capsulas.Setup(r => r.ObtenerLimites(4, null, default)).ReturnsAsync(limites);
        var mapa = await _servicio.ObtenerMapa(4, null, new(), null, 1, default);
        Assert.Equal(limites, mapa.Limites);
        Assert.Single(mapa.Items);
        Assert.True(mapa.HayMas);
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerMapa(4, null,
            new() { MinLatitud = -40, MaxLatitud = -30, MinLongitud = -70, MaxLongitud = -50 }, mapa.SiguienteCursor, 1, default));
    }

    [Fact]
    public async Task QueElMapaRechaceAreasIncompletasInvertidasOFueraDelMundo()
    {
        _capsulas.Setup(r => r.Obtener(4, null, default)).ReturnsAsync(new CapsulaDto { Id = 4 });
        foreach (var filtro in new[]
        {
            new ConsultaMapaCapsulaDto { MinLatitud = -40 },
            new() { MinLatitud = 20, MaxLatitud = 10, MinLongitud = -60, MaxLongitud = 0 },
            new() { MinLatitud = -91, MaxLatitud = 10, MinLongitud = -60, MaxLongitud = 0 },
            new() { MinLatitud = -40, MaxLatitud = 10, MinLongitud = double.NaN, MaxLongitud = 0 }
        })
            await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerMapa(4, null, filtro, null, 20, default));
    }

    #endregion
}
