using Moq;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class EtiquetadoPublicacionServicioTests
{
    private readonly Mock<IUsuarioRepositorio> _usuarioRepositorioMock = new();
    private readonly Mock<IPublicacionRepositorio> _publicacionRepositorioMock = new();
    private readonly Mock<IDriver> _neo4jDriverMock = new();
    private readonly EtiquetadoPublicacionServicio _servicio;

    public EtiquetadoPublicacionServicioTests()
    {
        _servicio = new EtiquetadoPublicacionServicio(
            _usuarioRepositorioMock.Object,
            _publicacionRepositorioMock.Object,
            _neo4jDriverMock.Object);
    }

    #region Etiquetado Exitoso

    [Fact]
    public async Task QueEtiquetarUsuariosCreeRelacionesEnNeo4jYElimineIdsDuplicados()
    {
        //Arrange
        _publicacionRepositorioMock
            .Setup(r => r.ExistePublicacionVisible(25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _usuarioRepositorioMock
            .Setup(r => r.ObtenerIdsExistentesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 10, 20 });

        string? queryEjecutada = null;
        object? parametrosEjecutados = null;
        ConfigurarNeo4j(2, (q, p) =>
        {
            queryEjecutada = q;
            parametrosEjecutados = p;
        });

        //Act
        var resultado = await _servicio.EtiquetarUsuarios(25, new[] { 10, 10, 20 });

        //Assert
        Assert.Equal(2, resultado);
        Assert.NotNull(queryEjecutada);
        Assert.Contains("ETIQUETADO_EN", queryEjecutada);
        Assert.NotNull(parametrosEjecutados);

        var paramTipo = parametrosEjecutados.GetType();
        var publicacionIdParam = paramTipo.GetProperty("publicacionId")?.GetValue(parametrosEjecutados)?.ToString();
        var usuariosIdsParam = paramTipo.GetProperty("usuariosIds")?.GetValue(parametrosEjecutados) as IEnumerable<int>;

        Assert.Equal("25", publicacionIdParam);
        Assert.NotNull(usuariosIdsParam);
        Assert.Equal(new[] { 10, 20 }, usuariosIdsParam);
    }

    #endregion

    #region Validaciones de Entrada

    [Fact]
    public async Task QuePublicacionInvalidaFalleAntesDeConsultarRepositorios()
    {
        //Arrange
        var publicacionIdInvalido = 0;

        //Act
        await Assert.ThrowsAsync<ArgumentException>(
            () => _servicio.EtiquetarUsuarios(publicacionIdInvalido, new[] { 10 }));

        //Assert
        _publicacionRepositorioMock.Verify(r => r.ExistePublicacionVisible(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _usuarioRepositorioMock.Verify(r => r.ObtenerIdsExistentesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        _neo4jDriverMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueListaVaciaFalleSinAccederALosRepositorios()
    {
        //Arrange
        var publicacionId = 25;

        //Act
        await Assert.ThrowsAsync<ArgumentException>(
            () => _servicio.EtiquetarUsuarios(publicacionId, Array.Empty<int>()));

        //Assert
        _publicacionRepositorioMock.Verify(r => r.ExistePublicacionVisible(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _usuarioRepositorioMock.Verify(r => r.ObtenerIdsExistentesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        _neo4jDriverMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueIdDeUsuarioInvalidoFalleAntesDeConsultarRepositorios()
    {
        //Arrange
        var publicacionId = 25;

        //Act
        await Assert.ThrowsAsync<ArgumentException>(
            () => _servicio.EtiquetarUsuarios(publicacionId, new[] { 0 }));

        //Assert
        _publicacionRepositorioMock.Verify(r => r.ExistePublicacionVisible(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _usuarioRepositorioMock.Verify(r => r.ObtenerIdsExistentesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        _neo4jDriverMock.VerifyNoOtherCalls();
    }

    #endregion

    #region Entidades Inexistentes

    [Fact]
    public async Task QuePublicacionInexistenteFalleSinConsultarUsuariosNiNeo4j()
    {
        //Arrange
        _publicacionRepositorioMock
            .Setup(r => r.ExistePublicacionVisible(25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        //Act
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _servicio.EtiquetarUsuarios(25, new[] { 10 }));

        //Assert
        _publicacionRepositorioMock.Verify(r => r.ExistePublicacionVisible(25, It.IsAny<CancellationToken>()), Times.Once);
        _usuarioRepositorioMock.Verify(r => r.ObtenerIdsExistentesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        _neo4jDriverMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueUsuarioInexistenteFalleSinCrearRelacionesEnNeo4j()
    {
        //Arrange
        _publicacionRepositorioMock
            .Setup(r => r.ExistePublicacionVisible(25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _usuarioRepositorioMock
            .Setup(r => r.ObtenerIdsExistentesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 10 });

        //Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _servicio.EtiquetarUsuarios(25, new[] { 10, 99 }));

        //Assert
        Assert.Contains("99", exception.Message);
        _neo4jDriverMock.VerifyNoOtherCalls();
    }

    #endregion

    #region Helpers Privados

    private void ConfigurarNeo4j(int relacionesCreadas, Action<string, object>? onRun = null)
    {
        var countersMock = new Mock<ICounters>();
        countersMock.Setup(c => c.RelationshipsCreated).Returns(relacionesCreadas);

        var summaryMock = new Mock<IResultSummary>();
        summaryMock.Setup(s => s.Counters).Returns(countersMock.Object);

        var cursorMock = new Mock<IResultCursor>();
        cursorMock.Setup(c => c.ConsumeAsync()).ReturnsAsync(summaryMock.Object);

        var runnerMock = new Mock<IAsyncQueryRunner>();
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((query, param) => onRun?.Invoke(query, param))
            .ReturnsAsync(cursorMock.Object);

        var sessionMock = new Mock<IAsyncSession>();
        sessionMock
            .Setup(s => s.ExecuteWriteAsync(
                It.IsAny<Func<IAsyncQueryRunner, Task<int>>>(),
                It.IsAny<Action<TransactionConfigBuilder>>()))
            .Returns<Func<IAsyncQueryRunner, Task<int>>, Action<TransactionConfigBuilder>>((work, _) => work(runnerMock.Object));

        _neo4jDriverMock
            .Setup(d => d.AsyncSession())
            .Returns(sessionMock.Object);
    }

    #endregion
}
