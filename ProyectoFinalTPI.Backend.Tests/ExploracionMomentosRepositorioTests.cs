using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ExploracionMomentosRepositorioTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly MomentoRepositorio _repositorio;

    public ExploracionMomentosRepositorioTests()
    {
        _context = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _repositorio = new(_context);
    }

    private async Task Preparar()
    {
        _context.Publicaciones.Add(new Publicacion
        {
            Id = 10, Usuario = new Usuario_Personal { Id = 7, Username = "autor", Email = "autor@test.com" },
            Lugar = new Lugar { Coordenadas = new Point(0, 0) { SRID = 4326 } },
            Fecha = new DateTime(1986, 6, 29, 0, 0, 0, DateTimeKind.Utc)
        });
        _context.Momentos.AddRange(new Momento { Id = 1, Nombre = "Mundial" },
            new Momento { Id = 2, Nombre = "La final", MomentoPadreId = 1 },
            new Momento { Id = 3, Nombre = "El gol", MomentoPadreId = 2 });
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task QueSePuedaBuscarPorNombreOAliasSinDuplicarElMomento()
    {
        //Arrange
        await Preparar();
        await _repositorio.AgregarAlias(new MomentoAlias { MomentoId = 1, Texto = "México 86", TextoNormalizado = "mexico 86" });

        //Act
        var porNombre = await _repositorio.Buscar("mundial", "mundial", null, 20, CancellationToken.None);
        var porAlias = await _repositorio.Buscar("méxico", "mexico", null, 20, CancellationToken.None);

        //Assert
        Assert.Equal(1, Assert.Single(porNombre).Id);
        Assert.Equal(1, Assert.Single(porAlias).Id);
    }

    [Fact]
    public async Task QueElDetalleIncluyaPadreHijosDirectosYAlias()
    {
        //Arrange
        await Preparar();
        await _repositorio.AgregarAlias(new MomentoAlias { MomentoId = 2, Texto = "Final del 86", TextoNormalizado = "final del 86" });

        //Act
        var result = await _repositorio.ObtenerDetalle(2, CancellationToken.None);

        //Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Padre!.Id);
        Assert.Equal(3, Assert.Single(result.Hijos).Id);
        Assert.Equal("Final del 86", Assert.Single(result.Alias));
    }

    [Fact]
    public async Task QueLaBusquedaRespeteElIdDeContinuacionYElLimite()
    {
        //Arrange
        await Preparar();

        //Act
        var result = await _repositorio.Buscar("", "", 1, 1, CancellationToken.None);

        //Assert
        Assert.Equal(2, Assert.Single(result).Id);
    }

    [Fact]
    public async Task QueLasPublicacionesDeUnMomentoIncluyanDescendientesSinRepetirRecuerdos()
    {
        //Arrange
        await Preparar();
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = 2 });
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = 3 });

        //Act
        var directos = await _repositorio.ObtenerPublicaciones(1, false, null, 20, CancellationToken.None);
        var descendientes = await _repositorio.ObtenerPublicaciones(1, true, null, 20, CancellationToken.None);

        //Assert
        Assert.Empty(directos);
        Assert.Equal(10, Assert.Single(descendientes).Id);
    }

    [Fact]
    public async Task QueLaCorreccionGuardeSeleccionRechazosPreferenciaYUnaNuevaVersion()
    {
        //Arrange
        await Preparar();
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = 1 });

        //Act
        var result = await _repositorio.Corregir(10, 7, new[] { 2 }, false, 1, CancellationToken.None);
        var automaticoPermitido = await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = 3 });

        //Assert
        Assert.Equal(2, result.VersionRelaciones);
        Assert.False(result.PermitirMomentosAutomaticos);
        Assert.True(result.PuedeCorregir);
        Assert.Equal(2, Assert.Single(result.Momentos).Momento.Id);
        Assert.Equal("autor", result.Momentos[0].Origen);
        var rechazado = await _repositorio.ObtenerAsociacion(10, 1);
        Assert.Equal(EstadoAsociacionMomento.Rechazado, rechazado!.Estado);
        Assert.Equal(OrigenAsociacionMomento.Autor, rechazado.Origen);
        Assert.False(automaticoPermitido);
        Assert.Null(await _repositorio.ObtenerAsociacion(10, 3));
    }

    [Fact]
    public async Task QueRepetirUnAnalisisSinCambiosNoModifiqueLaVersion()
    {
        //Arrange
        await Preparar();
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = 1, Confianza = 0.9, VersionAnalisis = "v1" });

        //Act
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = 1, Confianza = 0.9, VersionAnalisis = "v1" });
        var result = await _repositorio.ObtenerEstado(10, 7, CancellationToken.None);

        //Assert
        Assert.Equal(1, result!.VersionRelaciones);
        Assert.Single(result.Momentos);
    }

    [Fact]
    public async Task QueGuardarLaMismaSeleccionNoCambieLaVersionNiDupliqueAsociaciones()
    {
        //Arrange
        await Preparar();
        var primera = await _repositorio.Corregir(10, 7, new[] { 1 }, true, 0, CancellationToken.None);

        //Act
        var result = await _repositorio.Corregir(10, 7, new[] { 1 }, true, primera.VersionRelaciones, CancellationToken.None);

        //Assert
        Assert.Equal(primera.VersionRelaciones, result.VersionRelaciones);
        Assert.Equal(1, await _context.PublicacionesMomentos.CountAsync());
    }

    [Fact]
    public async Task QueSeRechaceUnaVersionViejaSinModificarLaSeleccionActual()
    {
        //Arrange
        await Preparar();
        await _repositorio.Corregir(10, 7, new[] { 1 }, true, 0, CancellationToken.None);

        //Act
        await Assert.ThrowsAsync<ConflictoRelacionesException>(() => _repositorio.Corregir(10, 7, new[] { 2 }, false, 0, CancellationToken.None));

        //Assert
        var result = await _repositorio.ObtenerEstado(10, 7, CancellationToken.None);
        Assert.Equal(1, Assert.Single(result!.Momentos).Momento.Id);
        Assert.True(result.PermitirMomentosAutomaticos);
    }

    [Fact]
    public async Task QueOtroUsuarioNoPuedaCambiarLasAsociaciones()
    {
        //Arrange
        await Preparar();

        //Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _repositorio.Corregir(10, 99, new[] { 1 }, false, 0, CancellationToken.None));

        //Assert
        Assert.Empty(await _context.PublicacionesMomentos.ToListAsync());
        Assert.True((await _context.Publicaciones.SingleAsync()).PermitirMomentosAutomaticos);
    }

    [Fact]
    public async Task QueUnMomentoInexistenteNoDejeUnaCorreccionParcial()
    {
        //Arrange
        await Preparar();

        //Act
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _repositorio.Corregir(10, 7, new[] { 1, 99 }, false, 0, CancellationToken.None));

        //Assert
        Assert.Empty(await _context.PublicacionesMomentos.ToListAsync());
        Assert.Equal(0, (await _context.Publicaciones.SingleAsync()).VersionRelaciones);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(7, true)]
    [InlineData(99, false)]
    public async Task QueElEstadoIndiqueSiElUsuarioPuedeCorregirSinExponerRechazos(int? usuarioId, bool puedeCorregir)
    {
        //Arrange
        await Preparar();
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = 1 });
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = 2, Origen = OrigenAsociacionMomento.Autor, Estado = EstadoAsociacionMomento.Rechazado });

        //Act
        var result = await _repositorio.ObtenerEstado(10, usuarioId, CancellationToken.None);

        //Assert
        Assert.Equal(puedeCorregir, result!.PuedeCorregir);
        Assert.Equal("automatico", Assert.Single(result.Momentos).Origen);
        Assert.Equal(2, result.VersionRelaciones);
    }

    public void Dispose() => _context.Dispose();
}
