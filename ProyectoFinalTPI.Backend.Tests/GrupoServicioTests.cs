using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class GrupoServicioTests
{
    private readonly Mock<IGrupoRepositorio> _grupos = new();
    private readonly Mock<IUsuarioRepositorio> _usuarios = new();
    private readonly Mock<IPublicacionServicio> _publicaciones = new();

    #region Creación de Grupos

    [Fact]
    public async Task QueCrearAgregueAlCreadorComoPrimerIntegrante()
    {
        //Arrange
        var creador = new Usuario_Personal { Id = 7, Username = "creador" };
        _usuarios.Setup(repositorio => repositorio.ObtenerPorIdAsync(creador.Id))
            .ReturnsAsync(creador);
        _grupos.Setup(repositorio => repositorio.Crear(
                It.IsAny<Grupo>(), It.IsAny<CancellationToken>()))
            .Callback<Grupo, CancellationToken>((grupo, _) => grupo.Id = 15)
            .ReturnsAsync(15);

        //Act
        var resultado = await CrearServicio().Crear(new Grupo
        {
            Nombre = "Viajes inolvidables",
            Descripcion = "Recuerdos de viajes",
            Decada = 1990,
            CreadorId = creador.Id
        });

        //Assert
        Assert.Equal(15, resultado.Id);
        Assert.True(resultado.EsCreador);
        Assert.True(resultado.EsIntegrante);
        Assert.Equal(1, resultado.CantidadIntegrantes);
        _grupos.Verify(repositorio => repositorio.Crear(
            It.Is<Grupo>(grupo => grupo.Integrantes.Count == 1 &&
                grupo.Integrantes.Single().UsuarioId == creador.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(1985)]
    [InlineData(1890)]
    [InlineData(2030)]
    public async Task QueCrearRechaceUnaDecadaInvalida(int decada)
    {
        //Arrange
        var grupo = new Grupo
        {
            Nombre = "Grupo de prueba",
            Descripcion = "Descripcion",
            Decada = decada,
            CreadorId = 10
        };

        //Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            CrearServicio().Crear(grupo));

        _grupos.Verify(repositorio => repositorio.Crear(
            It.IsAny<Grupo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Búsqueda de Grupos

    [Fact]
    public async Task QueBuscarNormaliceElTextoYMapeeLaRelacionDelUsuario()
    {
        //Arrange
        var grupo = CrearGrupo();
        _grupos.Setup(repositorio => repositorio.Buscar(
                "Grupo publico", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([grupo]);

        //Act
        var resultado = await CrearServicio().Buscar("  Grupo publico  ", 20, grupo.CreadorId);

        //Assert
        var encontrado = Assert.Single(resultado);
        Assert.True(encontrado.EsCreador);
        Assert.True(encontrado.EsIntegrante);
        _grupos.Verify(repositorio => repositorio.Buscar(
            "Grupo publico", 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("a", 20)]
    [InlineData("busqueda", 0)]
    [InlineData("busqueda", 51)]
    public async Task QueBuscarRechaceParametrosInvalidos(string texto, int limite)
    {
        //Arrange & Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CrearServicio().Buscar(texto, limite, null));

        _grupos.Verify(repositorio => repositorio.Buscar(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Membresía y Expulsión

    [Fact]
    public async Task QueUnirseSeaIdempotenteSiElUsuarioYaEsIntegrante()
    {
        //Arrange
        var grupo = CrearGrupo();
        _grupos.Setup(repositorio => repositorio.ObtenerPorId(
                grupo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grupo);
        _usuarios.Setup(repositorio => repositorio.ExisteUsuario(
                22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _grupos.Setup(repositorio => repositorio.EsIntegrante(
                grupo.Id, 22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        //Act
        await CrearServicio().Unirse(grupo.Id, 22);

        //Assert
        _grupos.Verify(repositorio => repositorio.AgregarIntegrante(
            It.IsAny<GrupoIntegrante>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueUnirseAgregueLaMembresiaSinInvitacion()
    {
        //Arrange
        var grupo = CrearGrupo();
        _grupos.Setup(repositorio => repositorio.ObtenerPorId(
                grupo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grupo);
        _usuarios.Setup(repositorio => repositorio.ExisteUsuario(
                22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _grupos.Setup(repositorio => repositorio.EsIntegrante(
                grupo.Id, 22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        //Act
        await CrearServicio().Unirse(grupo.Id, 22);

        //Assert
        _grupos.Verify(repositorio => repositorio.AgregarIntegrante(
            It.Is<GrupoIntegrante>(integrante =>
                integrante.GrupoId == grupo.Id && integrante.UsuarioId == 22),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueIntegranteComunNoPuedaExpulsarAOtroUsuario()
    {
        //Arrange
        var grupo = CrearGrupo();
        _grupos.Setup(repositorio => repositorio.ObtenerPorId(
                grupo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grupo);

        //Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearServicio().Expulsar(grupo.Id, 22, 30));

        _grupos.Verify(repositorio => repositorio.QuitarIntegrante(
            It.IsAny<GrupoIntegrante>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueCreadorPuedaExpulsarAUnIntegrante()
    {
        //Arrange
        var grupo = CrearGrupo();
        var integrante = new GrupoIntegrante { GrupoId = grupo.Id, UsuarioId = 22 };
        _grupos.Setup(repositorio => repositorio.ObtenerPorId(
                grupo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grupo);
        _grupos.Setup(repositorio => repositorio.ObtenerIntegrante(
                grupo.Id, integrante.UsuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(integrante);

        //Act
        await CrearServicio().Expulsar(grupo.Id, grupo.CreadorId, integrante.UsuarioId);

        //Assert
        _grupos.Verify(repositorio => repositorio.QuitarIntegrante(
            integrante, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueNoPermitaQueElCreadorAbandoneElGrupo()
    {
        //Arrange
        var grupo = CrearGrupo();
        _grupos.Setup(repositorio => repositorio.ObtenerPorId(
                grupo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grupo);

        //Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CrearServicio().Salir(grupo.Id, grupo.CreadorId));

        _grupos.Verify(repositorio => repositorio.QuitarIntegrante(
            It.IsAny<GrupoIntegrante>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Publicación en Grupos

    [Fact]
    public async Task QueIntegrantePuedaCrearUnRecuerdoAsociadoAlGrupo()
    {
        //Arrange
        var grupo = CrearGrupo();
        var publicacion = new Publicacion { Titulo = "Un recuerdo" };
        _grupos.Setup(repositorio => repositorio.ObtenerPorId(
                grupo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grupo);
        _grupos.Setup(repositorio => repositorio.EsIntegrante(
                grupo.Id, 22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _publicaciones.Setup(servicio => servicio.Crear(
                It.IsAny<Publicacion>(), null, null, null))
            .ReturnsAsync(99);

        //Act
        var id = await CrearServicio().CrearRecuerdo(grupo.Id, 22, publicacion);

        //Assert
        Assert.Equal(99, id);
        _publicaciones.Verify(servicio => servicio.Crear(
            It.Is<Publicacion>(recuerdo =>
                recuerdo.GrupoId == grupo.Id && recuerdo.UsuarioId == 22),
            null,
            null, null), Times.Once);
    }

    [Fact]
    public async Task QueUsuarioQueNoEsIntegranteNoPuedaPublicarEnElGrupo()
    {
        //Arrange
        var grupo = CrearGrupo();
        _grupos.Setup(repositorio => repositorio.ObtenerPorId(
                grupo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grupo);
        _grupos.Setup(repositorio => repositorio.EsIntegrante(
                grupo.Id, 22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        //Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearServicio().CrearRecuerdo(grupo.Id, 22, new Publicacion()));

        _publicaciones.Verify(servicio => servicio.Crear(
            It.IsAny<Publicacion>(), It.IsAny<Stream>(), It.IsAny<string>(), null), Times.Never);
    }

    #endregion

    #region Helpers Privados

    private GrupoServicio CrearServicio() =>
        new(_grupos.Object, _usuarios.Object, _publicaciones.Object);

    private static Grupo CrearGrupo()
    {
        var creador = new Usuario_Personal { Id = 10, Username = "creador" };
        return new Grupo
        {
            Id = 5,
            Nombre = "Grupo publico",
            Descripcion = "Descripcion",
            Decada = 1990,
            CreadorId = creador.Id,
            Creador = creador,
            Integrantes =
            [
                new GrupoIntegrante { GrupoId = 5, UsuarioId = creador.Id, Usuario = creador }
            ]
        };
    }

    #endregion
}
