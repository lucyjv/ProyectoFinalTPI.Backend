using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Grupos;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class OpcionesMultimediaTests
{
    #region Validación de Requests

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void QueLaMismaReglaDeAdjuntosSeApliqueAPublicacionesYGrupos(bool requerida, bool grupo)
    {
        //Arrange
        var servicios = new ServiceCollection();
        servicios.Configure<OpcionesPublicacion>(opciones => opciones.RequerirMultimedia = requerida);
        using var provider = servicios.BuildServiceProvider();
        IValidatableObject request = grupo ? new CrearRecuerdoGrupoRequest { Fecha = DateTime.UtcNow }
            : new CrearPublicacionRequest { Fecha = DateTime.UtcNow };

        //Act
        var errores = request.Validate(new ValidationContext(request, provider, null)).ToList();

        //Assert
        Assert.Equal(requerida, errores.Any(error => error.MemberNames.Contains("UrlArchivo")));
    }

    #endregion

    #region Protección en Llamadas al Servicio

    [Fact]
    public async Task QueExigirMultimediaProtejaLlamadasDirectasAlServicio()
    {
        //Arrange
        var usuariosMock = new Mock<IUsuarioRepositorio>();
        usuariosMock.Setup(repo => repo.ExisteUsuario(1, default)).ReturnsAsync(true);
        var publicacionesMock = new Mock<IPublicacionRepositorio>();
        var multimediaMock = new Mock<IMultimediaServicio>();
        var servicio = new PublicacionServicio(
            publicacionesMock.Object,
            usuariosMock.Object,
            multimediaMock.Object,
            Mock.Of<ISesionFypRepositorio>(),
            Mock.Of<ISeguimientoServicio>(),
            Options.Create(new FeedRecomendacionesOptions()),
            Mock.Of<INotificacionServicio>(),
            Mock.Of<ILogroServicio>(),
            Options.Create(new OpcionesPublicacion { RequerirMultimedia = true }));

        //Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.Crear(new Publicacion { UsuarioId = 1 }));
        publicacionesMock.Verify(repo => repo.Crear(It.IsAny<Publicacion>()), Times.Never);
        multimediaMock.Verify(s => s.SubirAsync(It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<MultimediaEnum>()), Times.Never);
    }

    #endregion
}
