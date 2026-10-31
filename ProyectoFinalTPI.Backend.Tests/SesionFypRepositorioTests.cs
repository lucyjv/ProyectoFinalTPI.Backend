using Microsoft.Extensions.Caching.Memory;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Repositorio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class SesionFypRepositorioTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly SesionFypRepositorio _repositorio;

    public SesionFypRepositorioTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _repositorio = new SesionFypRepositorio(_cache);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    #region Creación de Sesión FYP

    [Fact]
    public void QueCrearGuardeSesionEnCacheCuandoDatosSonValidos()
    {
        //Arrange
        int usuarioId = 1;
        var publicacionesIds = new List<int> { 10, 20, 30 };

        //Act
        var sesion = _repositorio.Crear(usuarioId, publicacionesIds);

        //Assert
        Assert.NotNull(sesion);
        Assert.NotEqual(Guid.Empty, sesion.Id);
        Assert.Equal(usuarioId, sesion.UsuarioId);
        Assert.Equal(publicacionesIds.Count, sesion.PublicacionesIds.Count);

        var clave = $"fyp:sesion:{sesion.Id:N}";
        Assert.True(_cache.TryGetValue(clave, out SesionFypDto? sesionEnCache));
        Assert.NotNull(sesionEnCache);
    }

    [Fact]
    public void QueCrearElimineIdsDuplicados()
    {
        //Arrange
        int usuarioId = 1;
        var publicacionesIds = new List<int> { 10, 20, 10, 30, 20 };

        //Act
        var sesion = _repositorio.Crear(usuarioId, publicacionesIds);

        //Assert
        Assert.NotNull(sesion);
        Assert.Equal(3, sesion.PublicacionesIds.Count);
        Assert.Contains(10, sesion.PublicacionesIds);
        Assert.Contains(20, sesion.PublicacionesIds);
        Assert.Contains(30, sesion.PublicacionesIds);
    }

    [Fact]
    public void QueCrearLanceArgumentNullExceptionCuandoListaEsNull()
    {
        //Arrange
        int usuarioId = 1;
        IReadOnlyCollection<int> publicacionesIds = null!;

        //Act & Assert
        Assert.Throws<ArgumentNullException>(() => _repositorio.Crear(usuarioId, publicacionesIds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void QueCrearLanceArgumentExceptionCuandoUsuarioIdEsInvalido(int usuarioIdInvalido)
    {
        //Arrange
        var publicacionesIds = new List<int> { 10, 20 };

        //Act & Assert
        var excepcion = Assert.Throws<ArgumentException>(() => _repositorio.Crear(usuarioIdInvalido, publicacionesIds));
        Assert.Contains("El ID del usuario debe ser mayor que cero", excepcion.Message);
    }

    [Fact]
    public void QueCrearLanceArgumentExceptionCuandoAlgunPublicacionIdEsInvalido()
    {
        //Arrange
        int usuarioId = 1;
        var publicacionesIds = new List<int> { 10, 0, 20 };

        //Act & Assert
        var excepcion = Assert.Throws<ArgumentException>(() => _repositorio.Crear(usuarioId, publicacionesIds));
        Assert.Contains("Los IDs de publicaciones deben ser mayores que cero", excepcion.Message);
    }

    #endregion

    #region Obtención de Sesión FYP

    [Fact]
    public void QueObtenerRetorneSesionCuandoExisteYPerteneceAlUsuario()
    {
        //Arrange
        int usuarioId = 1;
        var publicacionesIds = new List<int> { 10, 20, 30 };
        var sesionCreada = _repositorio.Crear(usuarioId, publicacionesIds);

        //Act
        var sesionObtenida = _repositorio.Obtener(sesionCreada.Id, usuarioId);

        //Assert
        Assert.NotNull(sesionObtenida);
        Assert.Equal(sesionCreada.Id, sesionObtenida.Id);
        Assert.Equal(usuarioId, sesionObtenida.UsuarioId);
    }

    [Fact]
    public void QueObtenerRetorneNullCuandoSesionNoExiste()
    {
        //Arrange
        var idInexistente = Guid.NewGuid();
        int usuarioId = 1;

        //Act
        var sesionObtenida = _repositorio.Obtener(idInexistente, usuarioId);

        //Assert
        Assert.Null(sesionObtenida);
    }

    [Fact]
    public void QueObtenerRetorneNullCuandoSesionEsDeOtroUsuario()
    {
        //Arrange
        int usuarioOriginalId = 1;
        int usuarioInvasorId = 2;
        var publicacionesIds = new List<int> { 10, 20 };
        var sesionCreada = _repositorio.Crear(usuarioOriginalId, publicacionesIds);

        //Act
        var sesionObtenida = _repositorio.Obtener(sesionCreada.Id, usuarioInvasorId);

        //Assert
        Assert.Null(sesionObtenida);
    }

    #endregion
}
