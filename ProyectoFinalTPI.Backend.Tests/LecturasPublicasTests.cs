using Moq;
using Xunit;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Cache;
namespace ProyectoFinalTPI.Backend.Tests;
public class LecturasPublicasTests
{
    [Theory]
    [InlineData("personal")]
    [InlineData("marca")]
    [InlineData("usuario")]
    public async Task QueSeDevuelvaElPerfilPublicoDesdeCacheSinConsultarElOrigenNiExponerCredenciales(string tipo)
    {
        //Arrange
        var origen = new Mock<IOrigenLecturasPublicas>(MockBehavior.Strict);
        var cache = new Mock<ICacheLecturasPublicas>(MockBehavior.Strict);
        cache.Setup(c => c.Obtener("perfil:7", It.IsAny<Func<Task<DatosPerfilPublico?>>>(), default))
            .ReturnsAsync(new DatosPerfilPublico(7, "Miguel", "foto", tipo, "Nombre", "Apellido", ReputacionEnum.Errante, 10));
        var consulta = new PerfilPublicoConsulta(origen.Object, cache.Object);
        //Act
        var result = await consulta.Obtener(7);
        //Assert
        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("Miguel", result.Username);
        Assert.Equal("foto", result.FotoPerfilUrl);
        Assert.True(string.IsNullOrEmpty(result.Contraseña));
        Assert.True(string.IsNullOrEmpty(result.Email));
        if (tipo == "personal") Assert.Equal("Apellido", Assert.IsType<Usuario_Personal>(result).Apellido);
        else if (tipo == "marca") Assert.Equal("Nombre", Assert.IsType<Usuario_Marca>(result).NombreEmpresa);
        else Assert.IsType<Usuario>(result);
        origen.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("personal")]
    [InlineData("marca")]
    [InlineData("usuario")]
    [InlineData("ausente")]
    public async Task QueLaFuenteDelPerfilSoloEntregueDatosPublicosALaCache(string tipo)
    {
        //Arrange
        Usuario? usuario = tipo switch {
            "personal" => new Usuario_Personal { Nombre = "Nombre", Apellido = "Apellido", PuntosNostalgia = 10 },
            "marca" => new Usuario_Marca { NombreEmpresa = "Marca", PuntosNostalgia = 20 },
            "usuario" => new Usuario(), _ => null };
        if (usuario is not null) { usuario.Id = 7; usuario.Username = "Miguel"; usuario.Email = "privado@example.com"; usuario.Contraseña = "hash-privado"; }
        var origen = new Mock<IOrigenLecturasPublicas>(MockBehavior.Strict);
        origen.Setup(o => o.ObtenerUsuario(7, default)).ReturnsAsync(usuario);
        DatosPerfilPublico? almacenado = null;
        var cache = new Mock<ICacheLecturasPublicas>(MockBehavior.Strict);
        cache.Setup(c => c.Obtener("perfil:7", It.IsAny<Func<Task<DatosPerfilPublico?>>>(), default))
            .Returns(async (string _, Func<Task<DatosPerfilPublico?>> fuente, CancellationToken ct) => almacenado = await fuente());
        var consulta = new PerfilPublicoConsulta(origen.Object, cache.Object);
        //Act
        var result = await consulta.Obtener(7);
        //Assert
        origen.Verify(o => o.ObtenerUsuario(7, default), Times.Once);
        if (tipo == "ausente") { Assert.Null(result); Assert.Null(almacenado); }
        else {
            Assert.NotNull(result);
            Assert.NotSame(usuario, result);
            Assert.Equal(tipo, almacenado!.Tipo);
            var json = System.Text.Json.JsonSerializer.Serialize(almacenado);
            Assert.DoesNotContain("hash-privado", json);
            Assert.DoesNotContain("privado@example.com", json);
        }
    }

    [Fact]
    public async Task QueNoSeDevuelvaUnResumenCacheadoSiLaPublicacionYaNoEsVisible()
    {
        //Arrange
        var origen = new Mock<IOrigenLecturasPublicas>(MockBehavior.Strict);
        origen.Setup(o => o.EsPublicacionVisible(5, default)).ReturnsAsync(false);
        var cache = new Mock<ICacheLecturasPublicas>(MockBehavior.Strict);
        var repositorio = new Mock<IPublicacionRepositorio>(MockBehavior.Strict);
        var consulta = new ResumenPublicacionConsulta(origen.Object, repositorio.Object, cache.Object);
        //Act
        var result = await consulta.Obtener(5);
        //Assert
        Assert.Null(result);
        cache.VerifyNoOtherCalls();
        repositorio.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueElResumenVisibleUseLaCacheOConsulteElRepositorioSegunCorresponda(bool acierto)
    {
        //Arrange
        using var cancelacion = new CancellationTokenSource();
        var ct = cancelacion.Token;
        var origen = new Mock<IOrigenLecturasPublicas>(MockBehavior.Strict);
        origen.Setup(o => o.EsPublicacionVisible(5, ct)).ReturnsAsync(true);
        var esperado = new PublicacionResumenDto { Id = 5, Titulo = "Recuerdo" };
        var repositorio = new Mock<IPublicacionRepositorio>(MockBehavior.Strict);
        if (!acierto) repositorio.Setup(r => r.ObtenerResumenPorId(5, ct)).ReturnsAsync(esperado);
        var cache = new Mock<ICacheLecturasPublicas>(MockBehavior.Strict);
        cache.Setup(c => c.Obtener("resumen:5", It.IsAny<Func<Task<PublicacionResumenDto?>>>(), ct))
            .Returns((string _, Func<Task<PublicacionResumenDto?>> fuente, CancellationToken token) => acierto ? Task.FromResult<PublicacionResumenDto?>(esperado) : fuente());
        var consulta = new ResumenPublicacionConsulta(origen.Object, repositorio.Object, cache.Object);
        //Act
        var result = await consulta.Obtener(5, ct);
        //Assert
        Assert.Same(esperado, result);
        origen.Verify(o => o.EsPublicacionVisible(5, ct), Times.Once);
        repositorio.Verify(r => r.ObtenerResumenPorId(5, ct), acierto ? Times.Never() : Times.Once());
    }
}
