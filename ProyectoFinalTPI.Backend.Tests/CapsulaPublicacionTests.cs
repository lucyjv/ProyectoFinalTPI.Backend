using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CapsulaPublicacionTests
{
    private readonly Mock<IPublicacionRepositorio> _publicaciones = new();
    private readonly Mock<IUsuarioRepositorio> _usuarios = new();
    private readonly Mock<ICapsulaRepositorio> _capsulas = new();
    private readonly Mock<IMultimediaServicio> _media = new();

    private PublicacionServicio CrearServicio() => new(_publicaciones.Object, _usuarios.Object, _media.Object,
        Mock.Of<ISesionFypRepositorio>(), Mock.Of<ISeguimientoServicio>(), Options.Create(new FeedRecomendacionesOptions()),
        Mock.Of<INotificacionServicio>(), Mock.Of<ILogroServicio>(), capsulaRepositorio: _capsulas.Object);

    [Fact]
    public async Task QueUnaCapsulaAjenaSeRechaceAntesDeSubirArchivosOGuardar()
    {
        // Arrange
        _capsulas.Setup(r => r.ValidarParaPublicar(10, It.IsAny<IReadOnlyCollection<int>>(), default))
            .ThrowsAsync(new ArgumentException("La cápsula no te pertenece."));

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => CrearServicio().Crear(new Publicacion { UsuarioId = 10 },
            null, null, null, null, new[] { 4 }));
        _media.VerifyNoOtherCalls();
        _publicaciones.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueUnaPublicacionNuevaSeAsocieUnaSolaVezACadaCapsula()
    {
        _usuarios.Setup(u => u.ExisteUsuario(10, default)).ReturnsAsync(true);
        _publicaciones.Setup(r => r.Crear(It.Is<Publicacion>(p => p.Capsulas.Select(c => c.CapsulaId).SequenceEqual(new[] { 4, 5 }))))
            .ReturnsAsync(30);
        var resultado = await CrearServicio().Crear(new Publicacion { UsuarioId = 10 }, null, null, null, null, new[] { 4, 4, 5 });
        Assert.Equal(30, resultado);
        _capsulas.Verify(r => r.ValidarParaPublicar(10, It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 4, 5 })), default), Times.Once);
        _publicaciones.Verify(r => r.Crear(It.IsAny<Publicacion>()), Times.Once);
    }

    [Fact]
    public async Task QueElHiloMantengaElOrdenSolicitadoYCompleteInteraccionesEnUnaConsulta()
    {
        _publicaciones.Setup(r => r.ObtenerPublicacionesPorIds(It.IsAny<IReadOnlyCollection<int>>(), default))
            .ReturnsAsync(new List<PublicacionDetalleDto> { new() { Id = 12 }, new() { Id = 11 } });
        _publicaciones.Setup(r => r.ObtenerResumenInteraccionesFeed(It.IsAny<IReadOnlyCollection<int>>(), 10, default))
            .ReturnsAsync(new Dictionary<int, ResumenInteraccionesFeedDto>
                { [11] = new() { CantidadLikes = 3, DioLike = true, Guardada = true } });
        var resultado = await CrearServicio().ObtenerDetallesPorIds(new[] { 11, 12, 11 }, 10);
        Assert.Equal(new[] { 11, 12 }, resultado.Select(p => p.Id));
        Assert.True(resultado[0].DioLike);
        Assert.True(resultado[0].Guardada);
        Assert.Equal(3, resultado[0].CantidadLikes);
        _publicaciones.Verify(r => r.ObtenerPublicacionesPorIds(It.IsAny<IReadOnlyCollection<int>>(), default), Times.Once);
        _publicaciones.Verify(r => r.ObtenerResumenInteraccionesFeed(It.IsAny<IReadOnlyCollection<int>>(), 10, default), Times.Once);
    }

    [Fact]
    public async Task QuePublicarEnUnGrupoConserveLaSeleccionDeCapsulasYElAutorAutenticado()
    {
        var grupos = new Mock<IGrupoRepositorio>();
        var publicaciones = new Mock<IPublicacionServicio>();
        grupos.Setup(r => r.ObtenerPorId(2, default)).ReturnsAsync(new Grupo { Id = 2 });
        grupos.Setup(r => r.EsIntegrante(2, 10, default)).ReturnsAsync(true);
        publicaciones.Setup(p => p.Crear(It.Is<Publicacion>(r => r.GrupoId == 2 && r.UsuarioId == 10), null, null, null, null,
            It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 4 })))).ReturnsAsync(30);
        var servicio = new GrupoServicio(grupos.Object, _usuarios.Object, publicaciones.Object);
        var id = await servicio.CrearRecuerdo(2, 10, new Publicacion { UsuarioId = 999 }, null, null, default, null, new[] { 4 });
        Assert.Equal(30, id);
        publicaciones.VerifyAll();
    }
}
