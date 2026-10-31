using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class SeguimientosControllerTests
{
    #region Seguir Usuario (POST /api/seguimientos)

    [Theory]
    [InlineData(ResultadoSeguimiento.Aplicado, HttpStatusCode.Created, "Ahora seguís a este usuario.")]
    [InlineData(ResultadoSeguimiento.SinCambios, HttpStatusCode.OK, "Ya seguías a este usuario.")]
    [InlineData(ResultadoSeguimiento.AutoSeguimiento, HttpStatusCode.BadRequest, "No podés seguirte a vos mismo.")]
    [InlineData(ResultadoSeguimiento.UsuarioNoEncontrado, HttpStatusCode.NotFound, "No se encontró uno o ambos usuarios.")]
    public async Task QueSeguirUsuarioDevuelvaCodigoYMensajeSegunResultado(
        ResultadoSeguimiento resultado,
        HttpStatusCode codigoEsperado,
        string mensajeEsperado)
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.SeguirUsuario(10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultado);
        using var client = factory.CreateClient();

        //Act
        using var response = await client.PostAsJsonAsync(
            "/api/seguimientos",
            new { idUsuario = 10, idUsuarioASeguir = 20 });
        var contenido = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.Equal(codigoEsperado, response.StatusCode);
        Assert.Contains(mensajeEsperado, contenido);
        factory.SeguimientoMock.Verify(s => s.SeguirUsuario(10, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueFallaInesperadaAlSeguirDevuelva500SinExponerDetalles()
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.SeguirUsuario(10, 20, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("detalle interno"));
        using var client = factory.CreateClient();

        //Act
        using var response = await client.PostAsJsonAsync(
            "/api/seguimientos",
            new { idUsuario = 10, idUsuarioASeguir = 20 });
        var contenido = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Ocurrió un error al procesar el seguimiento.", contenido);
        Assert.DoesNotContain("detalle interno", contenido);
    }

    [Fact]
    public async Task QueRequestConIdInvalidoAlSeguirDevuelva400SinInvocarServicio()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        //Act
        using var response = await client.PostAsJsonAsync(
            "/api/seguimientos",
            new { idUsuario = 0, idUsuarioASeguir = 20 });

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.SeguimientoMock.Verify(
            s => s.SeguirUsuario(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Dejar de Seguir (DELETE /api/seguimientos)

    [Theory]
    [InlineData(ResultadoSeguimiento.Aplicado, HttpStatusCode.NoContent)]
    [InlineData(ResultadoSeguimiento.SinCambios, HttpStatusCode.NoContent)]
    [InlineData(ResultadoSeguimiento.AutoSeguimiento, HttpStatusCode.BadRequest)]
    [InlineData(ResultadoSeguimiento.UsuarioNoEncontrado, HttpStatusCode.NotFound)]
    public async Task QueDejarDeSeguirDevuelvaCodigoSegunResultado(
        ResultadoSeguimiento resultado,
        HttpStatusCode codigoEsperado)
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.DejarDeSeguirUsuario(10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultado);
        using var client = factory.CreateClient();
        using var request = CrearRequestDelete(10, 20);

        //Act
        using var response = await client.SendAsync(request);

        //Assert
        Assert.Equal(codigoEsperado, response.StatusCode);
        factory.SeguimientoMock.Verify(s => s.DejarDeSeguirUsuario(10, 20, It.IsAny<CancellationToken>()), Times.Once);
        if (codigoEsperado == HttpStatusCode.NoContent)
        {
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task QueFallaInesperadaAlDejarDeSeguirDevuelva500SinExponerDetalles()
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.DejarDeSeguirUsuario(10, 20, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("detalle interno"));
        using var client = factory.CreateClient();
        using var request = CrearRequestDelete(10, 20);

        //Act
        using var response = await client.SendAsync(request);
        var contenido = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Ocurrió un error al procesar el seguimiento.", contenido);
        Assert.DoesNotContain("detalle interno", contenido);
    }

    [Fact]
    public async Task QueDeleteConIdInvalidoDevuelva400SinInvocarServicio()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = CrearRequestDelete(0, 20);

        //Act
        using var response = await client.SendAsync(request);

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.SeguimientoMock.Verify(
            s => s.DejarDeSeguirUsuario(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Listar Seguidos (GET /api/seguimientos/{id}/seguidos)

    [Fact]
    public async Task QueListarSeguidosDevuelvaLosUsuariosQueSigue()
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.ListarSeguidos(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CrearAutoresResumenEjemplo());
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/seguimientos/10/seguidos");

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seguidos = await response.Content.ReadFromJsonAsync<List<AutorResumenDto>>();
        Assert.NotNull(seguidos);
        Assert.Collection(
            seguidos,
            usuario =>
            {
                Assert.Equal(20, usuario.Id);
                Assert.Equal("usuario_20", usuario.Username);
            },
            usuario =>
            {
                Assert.Equal(30, usuario.Id);
                Assert.Equal("usuario_30", usuario.Username);
            });
        factory.SeguimientoMock.Verify(s => s.ListarSeguidos(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueListarSeguidosDevuelvaListaVaciaSiNoSigueAUsuarios()
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.ListarSeguidos(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AutorResumenDto>());
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/seguimientos/10/seguidos");

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadFromJsonAsync<List<AutorResumenDto>>() ?? new());
    }

    [Fact]
    public async Task QueListarSeguidosDevuelva404SiElUsuarioNoExiste()
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.ListarSeguidos(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<AutorResumenDto>?)null);
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/seguimientos/999/seguidos");
        var contenido = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No se encontro el usuario", contenido);
    }

    [Fact]
    public async Task QueListarSeguidosConIdInvalidoDevuelva400SinInvocarServicio()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/seguimientos/0/seguidos");

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.SeguimientoMock.Verify(s => s.ListarSeguidos(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueFallaInesperadaAlListarSeguidosDevuelva500SinExponerDetalles()
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.ListarSeguidos(10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("detalle interno"));
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/seguimientos/10/seguidos");
        var contenido = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Ocurrio un error al listar los usuarios seguidos", contenido);
        Assert.DoesNotContain("detalle interno", contenido);
    }

    #endregion

    #region Listar Seguidores (GET /api/seguimientos/{id}/seguidores)

    [Fact]
    public async Task QueListarSeguidoresDevuelvaLosUsuariosQueSiguenAlUsuario()
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.ListarSeguidores(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CrearAutoresResumenEjemplo());
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/seguimientos/10/seguidores");

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seguidores = await response.Content.ReadFromJsonAsync<List<AutorResumenDto>>();
        Assert.NotNull(seguidores);
        Assert.Collection(
            seguidores,
            usuario =>
            {
                Assert.Equal(20, usuario.Id);
                Assert.Equal("usuario_20", usuario.Username);
            },
            usuario =>
            {
                Assert.Equal(30, usuario.Id);
                Assert.Equal("usuario_30", usuario.Username);
            });
        factory.SeguimientoMock.Verify(s => s.ListarSeguidores(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueListarSeguidoresDevuelvaListaVaciaSiNadieSigueAlUsuario()
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.ListarSeguidores(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AutorResumenDto>());
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/seguimientos/10/seguidores");

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadFromJsonAsync<List<AutorResumenDto>>() ?? new());
    }

    [Fact]
    public async Task QueListarSeguidoresDevuelva404SiElUsuarioNoExiste()
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.ListarSeguidores(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<AutorResumenDto>?)null);
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/seguimientos/999/seguidores");
        var contenido = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No se encontro el usuario", contenido);
    }

    [Fact]
    public async Task QueListarSeguidoresConIdInvalidoDevuelva400SinInvocarServicio()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/seguimientos/0/seguidores");

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.SeguimientoMock.Verify(s => s.ListarSeguidores(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueFallaInesperadaAlListarSeguidoresDevuelva500SinExponerDetalles()
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.ListarSeguidores(10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("detalle interno"));
        using var client = factory.CreateClient();

        //Act
        using var response = await client.GetAsync("/api/seguimientos/10/seguidores");
        var contenido = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Ocurrio un error al listar los seguidores", contenido);
        Assert.DoesNotContain("detalle interno", contenido);
    }

    #endregion

    #region Seguir / Dejar de Seguir por Ruta (/api/seguimientos/{id})

    [Theory]
    [InlineData(ResultadoSeguimiento.Aplicado, HttpStatusCode.NoContent)]
    [InlineData(ResultadoSeguimiento.SinCambios, HttpStatusCode.NoContent)]
    [InlineData(ResultadoSeguimiento.AutoSeguimiento, HttpStatusCode.BadRequest)]
    [InlineData(ResultadoSeguimiento.UsuarioNoEncontrado, HttpStatusCode.NotFound)]
    public async Task QueDejarDeSeguirPorRutaDevuelvaCodigoSegunResultado(
        ResultadoSeguimiento resultado,
        HttpStatusCode codigoEsperado)
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.DejarDeSeguirUsuario(10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultado);
        using var client = factory.CreateClient();

        //Act
        using var response = await client.DeleteAsync("/api/seguimientos/20?idUsuario=10");

        //Assert
        Assert.Equal(codigoEsperado, response.StatusCode);
        factory.SeguimientoMock.Verify(s => s.DejarDeSeguirUsuario(10, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueDejarDeSeguirPorRutaSinUsuarioDevuelva401()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        //Act
        using var response = await client.DeleteAsync("/api/seguimientos/20");

        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        factory.SeguimientoMock.Verify(
            s => s.DejarDeSeguirUsuario(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(ResultadoSeguimiento.Aplicado, HttpStatusCode.Created)]
    [InlineData(ResultadoSeguimiento.SinCambios, HttpStatusCode.OK)]
    [InlineData(ResultadoSeguimiento.AutoSeguimiento, HttpStatusCode.BadRequest)]
    [InlineData(ResultadoSeguimiento.UsuarioNoEncontrado, HttpStatusCode.NotFound)]
    public async Task QueSeguirPorRutaDevuelvaCodigoSegunResultado(
        ResultadoSeguimiento resultado,
        HttpStatusCode codigoEsperado)
    {
        //Arrange
        await using var factory = new ApiFactory();
        factory.SeguimientoMock
            .Setup(s => s.SeguirUsuario(10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultado);
        using var client = factory.CreateClient();

        //Act
        using var response = await client.PostAsync("/api/seguimientos/20?idUsuario=10", null);

        //Assert
        Assert.Equal(codigoEsperado, response.StatusCode);
        factory.SeguimientoMock.Verify(s => s.SeguirUsuario(10, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueSeguirPorRutaSinUsuarioDevuelva401()
    {
        //Arrange
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        //Act
        using var response = await client.PostAsync("/api/seguimientos/20", null);

        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        factory.SeguimientoMock.Verify(
            s => s.SeguirUsuario(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Infraestructura y Helpers Privados

    private static List<AutorResumenDto> CrearAutoresResumenEjemplo()
    {
        return new List<AutorResumenDto>
        {
            new() { Id = 20, Username = "usuario_20" },
            new() { Id = 30, Username = "usuario_30" }
        };
    }

    private static HttpRequestMessage CrearRequestDelete(int idUsuario, int idUsuarioASeguir)
    {
        return new HttpRequestMessage(HttpMethod.Delete, "/api/seguimientos")
        {
            Content = JsonContent.Create(new { idUsuario, idUsuarioASeguir })
        };
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public Mock<ISeguimientoServicio> SeguimientoMock { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

                services.RemoveAll<ISeguimientoServicio>();
                services.AddSingleton(SeguimientoMock.Object);
            });
        }
    }

    #endregion
}
