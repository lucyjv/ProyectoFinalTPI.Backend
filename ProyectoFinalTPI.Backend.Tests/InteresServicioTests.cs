using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class InteresServicioTests
{
    private readonly Mock<IPublicacionRepositorio> _publicacionRepositorioMock = new();

    #region Validaciones de Usuario

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task QueObtenerInteresesConUsuarioInvalidoLanceArgumentException(int usuarioIdInvalido)
    {
        //Arrange
        var servicio = new InteresServicio(_publicacionRepositorioMock.Object);

        //Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.ObtenerIntereses(usuarioIdInvalido, CancellationToken.None));

        Assert.Equal("El ID del usuario debe ser mayor que cero.", ex.Message);
        _publicacionRepositorioMock.Verify(r =>
            r.ObtenerCantidadPorCategoriaDelUsuario(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Cálculo de Afinidades

    [Fact]
    public async Task QueObtenerInteresesSinPublicacionesDevuelvaColeccionVacia()
    {
        //Arrange
        const int usuarioId = 15;
        _publicacionRepositorioMock
            .Setup(r => r.ObtenerCantidadPorCategoriaDelUsuario(usuarioId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<CategoriaEnum, int>());

        var servicio = new InteresServicio(_publicacionRepositorioMock.Object);

        //Act
        var resultado = await servicio.ObtenerIntereses(usuarioId, CancellationToken.None);

        //Assert
        Assert.Empty(resultado);
        _publicacionRepositorioMock.Verify(r =>
            r.ObtenerCantidadPorCategoriaDelUsuario(usuarioId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueObtenerInteresesConCantidadesEnCeroDevuelvaColeccionVacia()
    {
        //Arrange
        const int usuarioId = 15;
        var cantidades = new Dictionary<CategoriaEnum, int>
        {
            [CategoriaEnum.Lugares] = 0,
            [CategoriaEnum.Cine] = 0
        };

        _publicacionRepositorioMock
            .Setup(r => r.ObtenerCantidadPorCategoriaDelUsuario(usuarioId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cantidades);

        var servicio = new InteresServicio(_publicacionRepositorioMock.Object);

        //Act
        var resultado = await servicio.ObtenerIntereses(usuarioId, CancellationToken.None);

        //Assert
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task QueObtenerInteresesCalculeAfinidadesOrdenadasDescendente()
    {
        //Arrange
        const int usuarioId = 15;
        var cantidades = new Dictionary<CategoriaEnum, int>
        {
            [CategoriaEnum.Lugares] = 3,
            [CategoriaEnum.Cine] = 1
        };

        _publicacionRepositorioMock
            .Setup(r => r.ObtenerCantidadPorCategoriaDelUsuario(usuarioId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cantidades);

        var servicio = new InteresServicio(_publicacionRepositorioMock.Object);

        //Act
        var resultado = await servicio.ObtenerIntereses(usuarioId, CancellationToken.None);

        //Assert
        Assert.Equal(2, resultado.Count);

        // Lugares: 3/4 = 0.75
        Assert.Equal(CategoriaEnum.Lugares, resultado[0].Categoria);
        Assert.Equal(0.75, resultado[0].Afinidad, 3);

        // Cine: 1/4 = 0.25
        Assert.Equal(CategoriaEnum.Cine, resultado[1].Categoria);
        Assert.Equal(0.25, resultado[1].Afinidad, 3);
    }

    #endregion
}
