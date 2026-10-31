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
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LaMismaReglaDeAdjuntosSeAplicaAPublicacionesYGrupos(bool requerida, bool grupo)
    {
        var servicios = new ServiceCollection();
        servicios.Configure<OpcionesPublicacion>(opciones => opciones.RequerirMultimedia = requerida);
        using var provider = servicios.BuildServiceProvider();
        IValidatableObject request = grupo ? new CrearRecuerdoGrupoRequest { Fecha = DateTime.UtcNow }
            : new CrearPublicacionRequest { Fecha = DateTime.UtcNow };
        var errores = request.Validate(new ValidationContext(request, provider, null)).ToList();
        Assert.Equal(requerida, errores.Any(error => error.MemberNames.Contains("UrlArchivo")));
    }

    [Fact]
    public async Task ExigirMultimediaTambienProtegeLasLlamadasDirectasAlServicio()
    {
        var usuarios = new Mock<IUsuarioRepositorio>();
        usuarios.Setup(repo => repo.ExisteUsuario(1, default)).ReturnsAsync(true);
        var publicaciones = new Mock<IPublicacionRepositorio>();
        var multimedia = new Mock<IMultimediaServicio>();
        var servicio = new PublicacionServicio(publicaciones.Object, usuarios.Object, multimedia.Object,
            Mock.Of<ISesionFypRepositorio>(), Mock.Of<ISeguimientoServicio>(), Options.Create(new FeedRecomendacionesOptions()),
            Mock.Of<INotificacionServicio>(), Mock.Of<ILogroServicio>(), Options.Create(new OpcionesPublicacion { RequerirMultimedia = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.Crear(new Publicacion { UsuarioId = 1 }));
        publicaciones.Verify(repo => repo.Crear(It.IsAny<Publicacion>()), Times.Never);
        multimedia.Verify(servicio => servicio.SubirAsync(It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<MultimediaEnum>()), Times.Never);
    }
}
