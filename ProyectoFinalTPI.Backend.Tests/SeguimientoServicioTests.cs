using Moq;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class SeguimientoServicioTests
{
    private readonly Mock<IUsuarioRepositorio> _usuarioRepositorioMock = new();
    private readonly Mock<IDriver> _driverMock = new();
    private readonly Mock<ILogroServicio> _logroServicioMock = new();
    private readonly Mock<INotificacionServicio> _notificacionServicioMock = new();
    private readonly SeguimientoServicio _servicio;

    public SeguimientoServicioTests()
    {
        _servicio = new SeguimientoServicio(
            _usuarioRepositorioMock.Object,
            _driverMock.Object,
            _logroServicioMock.Object,
            _notificacionServicioMock.Object);
    }

    #region Seguir Usuario

    [Fact]
    public async Task QueSeguirseASiMismoDevuelvaAutoSeguimientoSinConsultarUsuarios()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10);

        //Act
        var resultado = await _servicio.SeguirUsuario(10, 10);

        //Assert
        Assert.Equal(ResultadoSeguimiento.AutoSeguimiento, resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(10, 20)]
    [InlineData(20, 10)]
    public async Task QueSiFaltaCualquieraDeLosUsuariosDevuelvaUsuarioNoEncontradoAlSeguir(
        int idUsuarioExistente,
        int idUsuarioInexistente)
    {
        //Arrange
        ConfigurarUsuariosExistentes(idUsuarioExistente);

        //Act
        var resultado = await _servicio.SeguirUsuario(10, 20);

        //Assert
        Assert.Equal(ResultadoSeguimiento.UsuarioNoEncontrado, resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(idUsuarioExistente, It.IsAny<CancellationToken>()), Times.Once);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(idUsuarioInexistente, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Dejar de Seguir

    [Fact]
    public async Task QueDejarDeSeguirseASiMismoDevuelvaAutoSeguimientoSinConsultarUsuarios()
    {
        //Arrange
        ConfigurarUsuariosExistentes(10);

        //Act
        var resultado = await _servicio.DejarDeSeguirUsuario(10, 10);

        //Assert
        Assert.Equal(ResultadoSeguimiento.AutoSeguimiento, resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(10, 20)]
    [InlineData(20, 10)]
    public async Task QueSiFaltaCualquieraDeLosUsuariosDevuelvaUsuarioNoEncontradoAlDejarDeSeguir(
        int idUsuarioExistente,
        int idUsuarioInexistente)
    {
        //Arrange
        ConfigurarUsuariosExistentes(idUsuarioExistente);

        //Act
        var resultado = await _servicio.DejarDeSeguirUsuario(10, 20);

        //Assert
        Assert.Equal(ResultadoSeguimiento.UsuarioNoEncontrado, resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(idUsuarioExistente, It.IsAny<CancellationToken>()), Times.Once);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(idUsuarioInexistente, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Listado de Seguidos y Seguidores

    [Fact]
    public async Task QueListarSeguidosDevuelvaNullSiUsuarioNoExisteSinAccederANeo4j()
    {
        //Arrange
        ConfigurarUsuariosExistentes();

        //Act
        var resultado = await _servicio.ListarSeguidos(99);

        //Assert
        Assert.Null(resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(99, It.IsAny<CancellationToken>()), Times.Once);
        _driverMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueListarSeguidoresDevuelvaNullSiUsuarioNoExisteSinAccederANeo4j()
    {
        //Arrange
        ConfigurarUsuariosExistentes();

        //Act
        var resultado = await _servicio.ListarSeguidores(99);

        //Assert
        Assert.Null(resultado);
        _usuarioRepositorioMock.Verify(r => r.ExisteUsuario(99, It.IsAny<CancellationToken>()), Times.Once);
        _driverMock.VerifyNoOtherCalls();
    }

    #endregion

    #region Helpers Privados

    private void ConfigurarUsuariosExistentes(params int[] idsExistentes)
    {
        var set = idsExistentes.ToHashSet();
        _usuarioRepositorioMock
            .Setup(r => r.ExisteUsuario(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => set.Contains(id));
    }

    #endregion
}
