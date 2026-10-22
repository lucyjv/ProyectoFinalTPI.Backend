using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class MomentoRepositorioTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly MomentoRepositorio _repositorio;

    public MomentoRepositorioTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _repositorio = new MomentoRepositorio(_context);
    }

    [Fact]
    public async Task QueSePuedaCrearUnMomentoConFechasOpcionales()
    {
        //Arrange
        var momento = new Momento { Nombre = "Mundial de México 1986" };

        //Act
        var result = await _repositorio.Crear(momento);

        //Assert
        _context.ChangeTracker.Clear();
        var guardado = await _repositorio.ObtenerPorId(result);
        Assert.NotNull(guardado);
        Assert.Equal(momento.Nombre, guardado.Nombre);
        Assert.Null(guardado.FechaInicio);
        Assert.Null(guardado.FechaFin);
        Assert.Null(guardado.MomentoPadreId);
    }

    [Fact]
    public async Task QueNoSeCreeUnMomentoConUnPadreInexistente()
    {
        //Arrange
        var momento = new Momento { Nombre = "Gol de Maradona", MomentoPadreId = 99 };

        //Act
        var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _repositorio.Crear(momento));

        //Assert
        Assert.Contains("padre", result.Message);
        Assert.Empty(await _context.Momentos.ToListAsync());
    }

    [Fact]
    public async Task QueSePuedanConsultarLosHijosDirectosDeUnMomento()
    {
        //Arrange
        var padre = await _repositorio.Crear(new Momento { Nombre = "Mundial 1986" });
        var hijo = await _repositorio.Crear(new Momento { Nombre = "Argentina vs. Inglaterra", MomentoPadreId = padre });
        await _repositorio.Crear(new Momento { Nombre = "Gol a los ingleses", MomentoPadreId = hijo });

        //Act
        var result = await _repositorio.ObtenerHijos(padre);

        //Assert
        Assert.Equal(hijo, Assert.Single(result).Id);
    }

    [Fact]
    public async Task QueNoSePuedaGenerarUnCicloEntreVariosMomentos()
    {
        //Arrange
        var padre = await _repositorio.Crear(new Momento { Nombre = "Mundial" });
        var hijo = await _repositorio.Crear(new Momento { Nombre = "Partido", MomentoPadreId = padre });
        var nieto = await _repositorio.Crear(new Momento { Nombre = "Gol", MomentoPadreId = hijo });

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _repositorio.CambiarPadre(padre, nieto));

        //Assert
        Assert.Contains("ciclos", result.Message);
        Assert.Null((await _repositorio.ObtenerPorId(padre))!.MomentoPadreId);
    }

    [Fact]
    public async Task QueUnMomentoNoPuedaSerSuPropioPadre()
    {
        //Arrange
        var id = await _repositorio.Crear(new Momento { Nombre = "Mundial" });

        //Act
        var result = await Assert.ThrowsAsync<ArgumentException>(() => _repositorio.CambiarPadre(id, id));

        //Assert
        Assert.Contains("ciclos", result.Message);
        Assert.Null((await _repositorio.ObtenerPorId(id))!.MomentoPadreId);
    }

    [Fact]
    public async Task QueSePuedaCambiarYQuitarElPadreDeUnMomento()
    {
        //Arrange
        var padre = await _repositorio.Crear(new Momento { Nombre = "Mundial" });
        var hijo = await _repositorio.Crear(new Momento { Nombre = "Final" });

        //Act
        await _repositorio.CambiarPadre(hijo, padre);
        var vinculado = await _repositorio.ObtenerPorId(hijo);
        await _repositorio.CambiarPadre(hijo, null);
        var separado = await _repositorio.ObtenerPorId(hijo);

        //Assert
        Assert.Equal(padre, vinculado!.MomentoPadreId);
        Assert.Null(separado!.MomentoPadreId);
        Assert.Equal(2, await _context.Momentos.CountAsync());
    }

    [Fact]
    public async Task QueNoSeCambieElPadreDeUnMomentoInexistente()
    {
        //Arrange
        var idInexistente = 99;

        //Act
        var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _repositorio.CambiarPadre(idInexistente, null));

        //Assert
        Assert.Contains("no existe", result.Message);
    }

    [Fact]
    public async Task QueNoSeDupliqueUnAliasNormalizadoDentroDelMismoMomento()
    {
        //Arrange
        var id = await _repositorio.Crear(new Momento { Nombre = "Mundial de México 1986" });
        await _repositorio.AgregarAlias(new MomentoAlias { MomentoId = id, Texto = "México 86", TextoNormalizado = "mexico 86" });

        //Act
        await _repositorio.AgregarAlias(new MomentoAlias { MomentoId = id, Texto = "MEXICO 86", TextoNormalizado = "mexico 86" });

        //Assert
        var result = await _repositorio.ObtenerPorId(id);
        Assert.Equal("México 86", Assert.Single(result!.Alias).Texto);
        Assert.Equal(1, await _context.MomentosAlias.CountAsync());
    }

    [Fact]
    public async Task QueUnAliasAmbiguoPuedaExistirEnMomentosDiferentes()
    {
        //Arrange
        var primero = await _repositorio.Crear(new Momento { Nombre = "Argentina campeón 1986" });
        var segundo = await _repositorio.Crear(new Momento { Nombre = "Argentina campeón 2022" });

        //Act
        await _repositorio.AgregarAlias(new MomentoAlias { MomentoId = primero, Texto = "Campeones", TextoNormalizado = "campeones" });
        await _repositorio.AgregarAlias(new MomentoAlias { MomentoId = segundo, Texto = "Campeones", TextoNormalizado = "campeones" });

        //Assert
        Assert.Equal(2, await _context.MomentosAlias.CountAsync());
    }

    [Fact]
    public async Task QueNoSeAgregueUnAliasAUnMomentoInexistente()
    {
        //Arrange
        var alias = new MomentoAlias { MomentoId = 99, Texto = "México 86", TextoNormalizado = "mexico 86" };

        //Act
        var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _repositorio.AgregarAlias(alias));

        //Assert
        Assert.Contains("no existe", result.Message);
        Assert.Empty(await _context.MomentosAlias.ToListAsync());
    }

    [Theory]
    [InlineData(EstadoAsociacionMomento.Activo)]
    [InlineData(EstadoAsociacionMomento.Rechazado)]
    public async Task QueElAnalisisAutomaticoNoSobrescribaUnaDecisionDelAutor(EstadoAsociacionMomento estado)
    {
        //Arrange
        _context.Momentos.Add(new Momento { Id = 1, Nombre = "Mundial" });
        _context.Publicaciones.Add(new Publicacion { Id = 10 });
        await _context.SaveChangesAsync();
        await _repositorio.GuardarAsociacion(new PublicacionMomento
        {
            PublicacionId = 10, MomentoId = 1, Origen = OrigenAsociacionMomento.Autor, Estado = estado
        });

        //Act
        var result = await _repositorio.GuardarAsociacion(new PublicacionMomento
        {
            PublicacionId = 10, MomentoId = 1, Origen = OrigenAsociacionMomento.Automatico, Confianza = 0.95
        });

        //Assert
        var guardada = await _repositorio.ObtenerAsociacion(10, 1);
        Assert.False(result);
        Assert.Equal(OrigenAsociacionMomento.Autor, guardada!.Origen);
        Assert.Equal(estado, guardada.Estado);
        Assert.Null(guardada.Confianza);
        Assert.Equal(1, await _context.PublicacionesMomentos.CountAsync());
    }

    [Fact]
    public async Task QueSePuedaActualizarUnaAsociacionSinDuplicarlaNiCambiarSuFechaDeCreacion()
    {
        //Arrange
        _context.Momentos.Add(new Momento { Id = 1, Nombre = "Mundial" });
        _context.Publicaciones.Add(new Publicacion { Id = 10 });
        await _context.SaveChangesAsync();
        var creacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _repositorio.GuardarAsociacion(new PublicacionMomento
        {
            PublicacionId = 10, MomentoId = 1, Confianza = 0.8, FechaCreacion = creacion, VersionAnalisis = "v1"
        });

        //Act
        await _repositorio.GuardarAsociacion(new PublicacionMomento
        {
            PublicacionId = 10, MomentoId = 1, Confianza = 0.95, VersionAnalisis = "v2"
        });

        //Assert
        var result = await _repositorio.ObtenerAsociacion(10, 1);
        Assert.Equal(0.95, result!.Confianza);
        Assert.Equal("v2", result.VersionAnalisis);
        Assert.Equal(creacion, result.FechaCreacion);
        Assert.Equal(1, await _context.PublicacionesMomentos.CountAsync());
    }

    [Fact]
    public async Task QueElAutorPuedaConfirmarUnaAsociacionAutomatica()
    {
        //Arrange
        _context.Momentos.Add(new Momento { Id = 1, Nombre = "Mundial" });
        _context.Publicaciones.Add(new Publicacion { Id = 10 });
        await _context.SaveChangesAsync();
        await _repositorio.GuardarAsociacion(new PublicacionMomento
        {
            PublicacionId = 10, MomentoId = 1, Confianza = 0.9, VersionAnalisis = "v1"
        });

        //Act
        var result = await _repositorio.GuardarAsociacion(new PublicacionMomento
        {
            PublicacionId = 10, MomentoId = 1, Origen = OrigenAsociacionMomento.Autor
        });

        //Assert
        var guardada = await _repositorio.ObtenerAsociacion(10, 1);
        Assert.True(result);
        Assert.Equal(OrigenAsociacionMomento.Autor, guardada!.Origen);
        Assert.Null(guardada.Confianza);
        Assert.Null(guardada.VersionAnalisis);
    }

    [Fact]
    public async Task QueUnRecuerdoPuedaTenerVariosMomentosSinIncluirLosRechazados()
    {
        //Arrange
        var publicacion = new Publicacion { Id = 10 };
        _context.Publicaciones.Add(publicacion);
        var primero = await _repositorio.Crear(new Momento { Nombre = "Argentina campeón" });
        var segundo = await _repositorio.Crear(new Momento { Nombre = "Mundial" });
        var tercero = await _repositorio.Crear(new Momento { Nombre = "Otro partido" });
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = primero });
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = segundo });
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = tercero, Estado = EstadoAsociacionMomento.Rechazado });

        //Act
        var result = await _repositorio.ObtenerMomentosPublicacion(10);

        //Assert
        Assert.Equal(new[] { primero, segundo }, result.Select(m => m.Id));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, 99)]
    public async Task QueNoSeDevuelvanMomentosDeRecuerdosOcultosOReposts(bool oculto, int? originalId)
    {
        //Arrange
        _context.Publicaciones.Add(new Publicacion { Id = 10, EstaOculto = oculto, PublicacionOriginalId = originalId });
        var id = await _repositorio.Crear(new Momento { Nombre = "Mundial" });
        await _repositorio.GuardarAsociacion(new PublicacionMomento { PublicacionId = 10, MomentoId = id });

        //Act
        var result = await _repositorio.ObtenerMomentosPublicacion(10);

        //Assert
        Assert.Empty(result);
    }

    public void Dispose() => _context.Dispose();
}
